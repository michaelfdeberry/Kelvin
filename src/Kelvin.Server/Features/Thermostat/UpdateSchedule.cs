using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kelvin.Server.Features.Thermostat;

public record UpdateScheduleRequest(Guid Id, RunType Type, TimeOnly StartTime, TimeOnly EndTime, float TargetTemperatureC) : IRequest;

public static class UpdateScheduleErrors
{
  public static readonly Error ScheduleNotFound = new("UpdateSchedule.ScheduleNotFound", "The schedule with the specified ID was not found.");
}

public class UpdateScheduleHandler(KelvinContext context, IMemoryCache cache, IHandler<ValidateThermostatSafetyRequest> safetyValidator)
  : IHandler<UpdateScheduleRequest>
{
  public async Task<Result> HandleAsync(UpdateScheduleRequest request, CancellationToken ct = default)
  {
    var thermostat = await context.Thermostats.Include(t => t.SetPoints).Include(t => t.Schedules).FirstOrDefaultAsync(ct);

    var schedule = thermostat?.Schedules.FirstOrDefault(existing => existing.Id == request.Id);
    if (thermostat is null || schedule is null)
    {
      return Result.Failure(UpdateScheduleErrors.ScheduleNotFound);
    }

    var projectedSetPoints = thermostat.SetPoints.Select(setPoint => new SetPointProjection(setPoint.Id, setPoint.Type, setPoint.TargetTemperatureC));

    var projectedSchedules = thermostat.Schedules.Select(existing =>
      existing.Id == request.Id
        ? new ScheduleProjection(existing.Id, request.Type, request.StartTime, request.EndTime, request.TargetTemperatureC)
        : new ScheduleProjection(existing.Id, existing.Type, existing.StartTime, existing.EndTime, existing.TargetTemperatureC)
    );

    var safetyResult = await safetyValidator.HandleAsync(
      new ValidateThermostatSafetyRequest(new ThermostatProjection(thermostat.HysteresisC, projectedSetPoints, projectedSchedules)),
      ct
    );

    if (safetyResult.IsFailure)
    {
      return safetyResult;
    }

    schedule.Type = request.Type;
    schedule.StartTime = request.StartTime;
    schedule.EndTime = request.EndTime;
    schedule.TargetTemperatureC = request.TargetTemperatureC;

    await context.SaveChangesAsync(ct);
    cache.Remove(ThermostatCache.Key);

    return Result.Success();
  }
}

public class UpdateScheduleEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPut(
        "/api/thermostat/schedules/{id:guid}",
        async (Guid id, CreateScheduleRequest request, IHandler<UpdateScheduleRequest> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(
            new UpdateScheduleRequest(id, request.Type, request.StartTime, request.EndTime, request.TargetTemperatureC),
            ct
          );

          if (result.IsFailure)
          {
            if (result.Error == UpdateScheduleErrors.ScheduleNotFound)
            {
              return Results.NotFound(result.Error);
            }

            return Results.BadRequest(result.Error);
          }

          return Results.NoContent();
        }
      )
      .WithName("UpdateSchedule")
      .WithTags("Thermostats");
  }
}

public class UpdateScheduleRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<UpdateScheduleRequest>, UpdateScheduleHandler>();
  }
}
