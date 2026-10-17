using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kelvin.Server.Features.Thermostat;

public record SetPointInput(Guid? Id, RunType Type, float TargetTemperatureC);

public record ScheduleInput(Guid? Id, RunType Type, TimeOnly StartTime, TimeOnly EndTime, float TargetTemperatureC);

public record UpdateThermostatSettingsRequest(
  float? HeatingLockoutC,
  float? CoolingLockoutC,
  IEnumerable<SetPointInput> SetPoints,
  IEnumerable<ScheduleInput> Schedules
) : IRequest;

public static class UpdateThermostatSettingsErrors
{
  public static readonly Error ThermostatNotFound = new(
    "UpdateThermostatSettings.ThermostatNotFound",
    "The thermostat with the specified ID was not found."
  );
}

public class UpdateThermostatSettingsHandler(
  KelvinContext context,
  IMemoryCache cache,
  IHandler<ValidateThermostatSafetyRequest> safetyValidator,
  IDispatcher dispatcher
) : IHandler<UpdateThermostatSettingsRequest>
{
  public async Task<Result> HandleAsync(UpdateThermostatSettingsRequest request, CancellationToken ct = default)
  {
    var thermostat = await context.Thermostats.Include(t => t.SetPoints).Include(t => t.Schedules).FirstOrDefaultAsync(ct);
    if (thermostat is null)
    {
      return Result.Failure(UpdateThermostatSettingsErrors.ThermostatNotFound);
    }

    // Set the incoming lockout values before validating so ValidateThermostatSafetyHandler - which re-reads the
    // thermostat off the same tracked context instance - sees the requested values rather than the persisted ones.
    thermostat.HeatingLockoutC = request.HeatingLockoutC;
    thermostat.CoolingLockoutC = request.CoolingLockoutC;

    var projectedSetPoints = request.SetPoints.Select(setPoint => new SetPointProjection(setPoint.Id, setPoint.Type, setPoint.TargetTemperatureC));
    var projectedSchedules = request.Schedules.Select(schedule => new ScheduleProjection(
      schedule.Id,
      schedule.Type,
      schedule.StartTime,
      schedule.EndTime,
      schedule.TargetTemperatureC
    ));

    var safetyResult = await safetyValidator.HandleAsync(
      new ValidateThermostatSafetyRequest(new ThermostatProjection(thermostat.HysteresisC, projectedSetPoints, projectedSchedules)),
      ct
    );

    if (safetyResult.IsFailure)
    {
      return safetyResult;
    }

    ApplySetPoints(context, thermostat, request.SetPoints);
    ApplySchedules(context, thermostat, request.Schedules);

    await context.SaveChangesAsync(ct);
    cache.Remove(ThermostatCache.Key);

    await dispatcher.PublishAsync(new ThermostatConfigChangedEvent(), ct);
    return Result.Success();
  }

  private static void ApplySetPoint(KelvinContext context, Models.Thermostat thermostat, SetPointInput? setPoint)
  {
    if (setPoint is null)
      return;

    var existingSetPoints = thermostat.SetPoints.Where(x => x.Type == setPoint.Type && x.DeletedAt is null).ToList();
    var updatedSetPoint = existingSetPoints.FirstOrDefault(x => x.Id == setPoint.Id);

    if (updatedSetPoint is null)
    {
      updatedSetPoint = new SetPoint
      {
        Type = setPoint.Type,
        ThermostatId = thermostat.Id,
        TargetTemperatureC = setPoint.TargetTemperatureC,
      };
      thermostat.SetPoints.Add(updatedSetPoint);
    }
    else
    {
      updatedSetPoint.TargetTemperatureC = setPoint.TargetTemperatureC;
      existingSetPoints.Remove(updatedSetPoint);
    }

    foreach (var existing in existingSetPoints)
    {
      thermostat.SetPoints.Remove(existing);
      context.SetPoints.Remove(existing);
    }
  }

  private static void ApplySetPoints(KelvinContext context, Models.Thermostat thermostat, IEnumerable<SetPointInput> setPoints)
  {
    // there can never be more than one heating or cooling set point in the incoming request.
    var heatingSetPoint = setPoints.FirstOrDefault(x => x.Type == RunType.Heating);
    var coolingSetPoint = setPoints.FirstOrDefault(x => x.Type == RunType.Cooling);

    ApplySetPoint(context, thermostat, heatingSetPoint);
    ApplySetPoint(context, thermostat, coolingSetPoint);
  }

  private static void ApplySchedules(KelvinContext context, Models.Thermostat thermostat, IEnumerable<ScheduleInput> schedules)
  {
    var activeSchedules = thermostat.Schedules.Where(existing => existing.DeletedAt is null).ToList();
    var incomingIds = schedules.Where(schedule => schedule.Id.HasValue).Select(schedule => schedule.Id!.Value).ToHashSet();

    foreach (var removed in activeSchedules.Where(existing => !incomingIds.Contains(existing.Id)))
    {
      thermostat.Schedules.Remove(removed);
      context.Schedules.Remove(removed);
    }

    foreach (var input in schedules)
    {
      var existing = input.Id.HasValue ? activeSchedules.FirstOrDefault(schedule => schedule.Id == input.Id) : null;
      if (existing is not null)
      {
        existing.Type = input.Type;
        existing.StartTime = input.StartTime;
        existing.EndTime = input.EndTime;
        existing.TargetTemperatureC = input.TargetTemperatureC;
      }
      else
      {
        thermostat.Schedules.Add(
          new Schedule
          {
            ThermostatId = thermostat.Id,
            Type = input.Type,
            StartTime = input.StartTime,
            EndTime = input.EndTime,
            TargetTemperatureC = input.TargetTemperatureC,
          }
        );
      }
    }
  }
}

public class UpdateThermostatSettingsEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPut(
        "/api/thermostat/settings",
        async (UpdateThermostatSettingsRequest request, IHandler<UpdateThermostatSettingsRequest> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(request, ct);
          if (result.IsFailure)
          {
            if (result.Error == UpdateThermostatSettingsErrors.ThermostatNotFound)
            {
              return Results.NotFound(result.Error);
            }

            return Results.BadRequest(result.Error);
          }

          return Results.NoContent();
        }
      )
      .WithName("UpdateThermostatSettings")
      .WithTags("Thermostats");
  }
}

public class UpdateThermostatSettingsRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<UpdateThermostatSettingsRequest>, UpdateThermostatSettingsHandler>();
  }
}
