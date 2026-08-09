using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kelvin.Server.Features.Thermostat;

public record CreateScheduleRequest(RunType Type, TimeOnly StartTime, TimeOnly EndTime, float TargetTemperatureC) : IRequest<CreateScheduleResponse>;

public record CreateScheduleResponse(Guid Id, RunType Type, TimeOnly StartTime, TimeOnly EndTime, float TargetTemperatureC);

public static class CreateScheduleErrors
{
  public static readonly Error ThermostatNotFound = new("CreateSchedule.ThermostatNotFound", "The thermostat with the specified ID was not found.");
}

public class CreateScheduleHandler(KelvinContext context, IMemoryCache cache, IHandler<ValidateThermostatSafetyRequest> safetyValidator)
  : IHandler<CreateScheduleRequest, CreateScheduleResponse>
{
  public async Task<Result<CreateScheduleResponse>> HandleAsync(CreateScheduleRequest request, CancellationToken ct = default)
  {
    var thermostat = await context.Thermostats.Include(t => t.SetPoints).Include(t => t.Schedules).FirstOrDefaultAsync(ct);

    if (thermostat is null)
    {
      return Result<CreateScheduleResponse>.Failure(CreateScheduleErrors.ThermostatNotFound);
    }

    var projectedSetPoints = thermostat.SetPoints.Select(setPoint => new SetPointProjection(setPoint.Id, setPoint.Type, setPoint.TargetTemperatureC));

    var projectedSchedules = thermostat
      .Schedules.Select(existing => new ScheduleProjection(
        existing.Id,
        existing.Type,
        existing.StartTime,
        existing.EndTime,
        existing.TargetTemperatureC
      ))
      .Append(new ScheduleProjection(null, request.Type, request.StartTime, request.EndTime, request.TargetTemperatureC));

    var safetyResult = await safetyValidator.HandleAsync(
      new ValidateThermostatSafetyRequest(new ThermostatProjection(thermostat.HysteresisC, projectedSetPoints, projectedSchedules)),
      ct
    );

    if (safetyResult.IsFailure)
    {
      return Result<CreateScheduleResponse>.Failure(safetyResult.Error);
    }

    var schedule = new Schedule
    {
      ThermostatId = thermostat.Id,
      Type = request.Type,
      StartTime = request.StartTime,
      EndTime = request.EndTime,
      TargetTemperatureC = request.TargetTemperatureC,
    };

    context.Schedules.Add(schedule);
    await context.SaveChangesAsync(ct);
    cache.Remove(ThermostatCache.Key);

    return Result<CreateScheduleResponse>.Success(
      new CreateScheduleResponse(schedule.Id, schedule.Type, schedule.StartTime, schedule.EndTime, schedule.TargetTemperatureC)
    );
  }
}

public class CreateScheduleEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost(
        "/api/thermostat/schedules",
        async (CreateScheduleRequest request, IHandler<CreateScheduleRequest, CreateScheduleResponse> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(request, ct);
          if (result.IsFailure)
          {
            if (result.Error == CreateScheduleErrors.ThermostatNotFound)
            {
              return Results.Json(result.Error, statusCode: StatusCodes.Status412PreconditionFailed);
            }

            return Results.BadRequest(result.Error);
          }

          return Results.Ok(result.Value);
        }
      )
      .WithName("CreateSchedule")
      .WithTags("Thermostats");
  }
}

public class CreateScheduleRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<CreateScheduleRequest, CreateScheduleResponse>, CreateScheduleHandler>();
  }
}
