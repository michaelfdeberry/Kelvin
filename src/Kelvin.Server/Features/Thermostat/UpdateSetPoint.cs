using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kelvin.Server.Features.Thermostat;

public record UpdateSetPointRequest(Guid Id, RunType Type, float TargetTemperatureC) : IRequest;

public static class UpdateSetPointErrors
{
  public static readonly Error SetPointNotFound = new("UpdateSetPoint.SetPointNotFound", "The set point with the specified ID was not found.");
}

public class UpdateSetPointHandler(KelvinContext context, IMemoryCache cache, IHandler<ValidateThermostatSafetyRequest> safetyValidator)
  : IHandler<UpdateSetPointRequest>
{
  public async Task<Result> HandleAsync(UpdateSetPointRequest request, CancellationToken ct = default)
  {
    var thermostat = await context.Thermostats.Include(t => t.SetPoints).Include(t => t.Schedules).FirstOrDefaultAsync(ct);

    var setPoint = thermostat?.SetPoints.FirstOrDefault(sp => sp.Id == request.Id);
    if (thermostat is null || setPoint is null)
    {
      return Result.Failure(UpdateSetPointErrors.SetPointNotFound);
    }

    var projectedSetPoints = thermostat.SetPoints.Select(existing =>
      existing.Id == request.Id
        ? new SetPointProjection(existing.Id, request.Type, request.TargetTemperatureC)
        : new SetPointProjection(existing.Id, existing.Type, existing.TargetTemperatureC)
    );

    var projectedSchedules = thermostat.Schedules.Select(schedule => new ScheduleProjection(
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

    setPoint.Type = request.Type;
    setPoint.TargetTemperatureC = request.TargetTemperatureC;

    await context.SaveChangesAsync(ct);
    cache.Remove(ThermostatCache.Key);

    return Result.Success();
  }
}

public class UpdateSetPointEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPut(
        "/api/thermostat/set-points/{id:guid}",
        async (Guid id, CreateSetPointRequest request, IHandler<UpdateSetPointRequest> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new UpdateSetPointRequest(id, request.Type, request.TargetTemperatureC), ct);

          if (result.IsFailure)
          {
            if (result.Error == UpdateSetPointErrors.SetPointNotFound)
            {
              return Results.NotFound(result.Error);
            }

            return Results.BadRequest(result.Error);
          }

          return Results.NoContent();
        }
      )
      .WithName("UpdateSetPoint")
      .WithTags("Thermostats");
  }
}

public class UpdateSetPointRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<UpdateSetPointRequest>, UpdateSetPointHandler>();
  }
}
