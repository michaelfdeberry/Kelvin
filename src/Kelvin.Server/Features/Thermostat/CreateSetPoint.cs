using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kelvin.Server.Features.Thermostat;

public record CreateSetPointRequest(RunType Type, float TargetTemperatureC) : IRequest<CreateSetPointResponse>;

public record CreateSetPointResponse(Guid Id, RunType Type, float TargetTemperatureC);

public static class CreateSetPointErrors
{
  public static readonly Error ThermostatNotFound = new("CreateSetPoint.ThermostatNotFound", "The thermostat with the specified ID was not found.");
}

public class CreateSetPointHandler(KelvinContext context, IMemoryCache cache, IHandler<ValidateThermostatSafetyRequest> safetyValidator)
  : IHandler<CreateSetPointRequest, CreateSetPointResponse>
{
  public async Task<Result<CreateSetPointResponse>> HandleAsync(CreateSetPointRequest request, CancellationToken ct = default)
  {
    var thermostat = await context.Thermostats.Include(t => t.SetPoints).Include(t => t.Schedules).FirstOrDefaultAsync(ct);

    if (thermostat is null)
    {
      return Result<CreateSetPointResponse>.Failure(CreateSetPointErrors.ThermostatNotFound);
    }

    var projectedSetPoints = thermostat
      .SetPoints.Select(setPoint => new SetPointProjection(setPoint.Id, setPoint.Type, setPoint.TargetTemperatureC))
      .Append(new SetPointProjection(null, request.Type, request.TargetTemperatureC));

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
      return Result<CreateSetPointResponse>.Failure(safetyResult.Error);
    }

    var setPoint = new SetPoint
    {
      ThermostatId = thermostat.Id,
      Type = request.Type,
      TargetTemperatureC = request.TargetTemperatureC,
    };

    context.SetPoints.Add(setPoint);
    await context.SaveChangesAsync(ct);
    cache.Remove(ThermostatCache.Key);

    return Result<CreateSetPointResponse>.Success(new CreateSetPointResponse(setPoint.Id, setPoint.Type, setPoint.TargetTemperatureC));
  }
}

public class CreateSetPointEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost(
        "/api/thermostat/set-points",
        async (CreateSetPointRequest request, IHandler<CreateSetPointRequest, CreateSetPointResponse> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(request, ct);
          if (result.IsFailure)
          {
            if (result.Error == CreateSetPointErrors.ThermostatNotFound)
            {
              return Results.Json(result.Error, statusCode: StatusCodes.Status412PreconditionFailed);
            }

            return Results.BadRequest(result.Error);
          }

          return Results.Ok(result.Value);
        }
      )
      .WithName("CreateSetPoint")
      .WithTags("Thermostats");
  }
}

public class CreateSetPointRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<CreateSetPointRequest, CreateSetPointResponse>, CreateSetPointHandler>();
  }
}
