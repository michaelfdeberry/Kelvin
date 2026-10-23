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

public class UpdateThermostatSettingsHandler(KelvinContext context, IMemoryCache cache, IDispatcher dispatcher)
  : IHandler<UpdateThermostatSettingsRequest>
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
    // Keep existing values if not provided.
    thermostat.HeatingLockoutC = request.HeatingLockoutC ?? thermostat.HeatingLockoutC;
    thermostat.CoolingLockoutC = request.CoolingLockoutC ?? thermostat.CoolingLockoutC;

    var projectedSetPoints = request.SetPoints.Select(setPoint => new SetPointProjection(setPoint.Id, setPoint.Type, setPoint.TargetTemperatureC));
    var projectedSchedules = request.Schedules.Select(schedule => new ScheduleProjection(
      schedule.Id,
      schedule.Type,
      schedule.StartTime,
      schedule.EndTime,
      schedule.TargetTemperatureC
    ));

    var safetyResult = await dispatcher.DispatchAsync(
      new ValidateThermostatSafetyRequest(new ThermostatProjection(thermostat.HysteresisC, projectedSetPoints, projectedSchedules)),
      ct
    );

    if (safetyResult.IsFailure)
    {
      return safetyResult;
    }

    var heatingSetPoint = request.SetPoints.FirstOrDefault(x => x.Type == RunType.Heating);
    var coolingSetPoint = request.SetPoints.FirstOrDefault(x => x.Type == RunType.Cooling);

    ApplySetPoint(thermostat, heatingSetPoint);
    ApplySetPoint(thermostat, coolingSetPoint);

    ApplyModeSchedules(thermostat, RunType.Heating, request.Schedules);
    ApplyModeSchedules(thermostat, RunType.Cooling, request.Schedules);

    await context.SaveChangesAsync(ct);
    cache.Remove(ThermostatCache.Key);

    await dispatcher.PublishAsync(new ThermostatConfigChangedEvent(), ct);
    return Result.Success();
  }

  private void ApplySetPoint(Models.Thermostat thermostat, SetPointInput? setPoint)
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

  private void ApplyModeSchedules(Models.Thermostat thermostat, RunType type, IEnumerable<ScheduleInput> schedules)
  {
    var schedulesForMode = schedules.Where(x => x.Type == type);

    // Skip if not in automatic mode and no schedules exist for this mode.
    // This retains the schedule for the alternative mode.
    if (thermostat.Mode != RunMode.Automatic && !schedulesForMode.Any())
      return;

    var existing = thermostat.Schedules.Where(x => x.DeletedAt is null && x.Type == type).ToList();
    foreach (var s in existing)
    {
      thermostat.Schedules.Remove(s);
      context.Schedules.Remove(s);
    }

    foreach (var s in schedulesForMode)
    {
      thermostat.Schedules.Add(
        new Schedule
        {
          ThermostatId = thermostat.Id,
          Type = type,
          StartTime = s.StartTime,
          EndTime = s.EndTime,
          TargetTemperatureC = s.TargetTemperatureC,
        }
      );
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
