using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Thermostat;

public record GetSchedulesRequest() : IRequest<GetSchedulesResponse>;

public record ScheduleResponse(
  Guid Id,
  RunType Type,
  bool Enabled,
  TimeOnly StartTime,
  TimeOnly EndTime,
  float TargetTemperatureC,
  float? ActivationTemperatureC
);

public record GetSchedulesResponse(IEnumerable<ScheduleResponse> Schedules);

public class GetSchedulesHandler(KelvinContext context) : IHandler<GetSchedulesRequest, GetSchedulesResponse>
{
  public async Task<Result<GetSchedulesResponse>> HandleAsync(GetSchedulesRequest request, CancellationToken ct = default)
  {
    var schedules = await context.Schedules.ToListAsync(ct);
    return Result<GetSchedulesResponse>.Success(
      new GetSchedulesResponse([
        .. schedules.Select(s => new ScheduleResponse(
          s.Id,
          s.Type,
          s.Enabled,
          s.StartTime,
          s.EndTime,
          s.TargetTemperatureC,
          s.ActivationTemperatureC
        )),
      ])
    );
  }
}

public class GetSchedulesEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet(
        "/api/thermostat/schedules",
        async (IHandler<GetSchedulesRequest, GetSchedulesResponse> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new GetSchedulesRequest(), ct);

          if (result.IsFailure)
          {
            return Results.InternalServerError(result.Error);
          }

          return Results.Ok(result.Value);
        }
      )
      .WithName("GetSchedules")
      .WithTags("Thermostats");
  }
}

public class GetSchedulesFeatureRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<GetSchedulesRequest, GetSchedulesResponse>, GetSchedulesHandler>();
  }
}
