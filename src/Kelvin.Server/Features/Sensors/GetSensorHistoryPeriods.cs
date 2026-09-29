using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Sensors;

public record GetSensorHistoryPeriodsRequest(DateTimeOffset From, DateTimeOffset To, Guid? SensorId = null, int? PeriodSeconds = null)
  : IRequest<GetSensorHistoryPeriodsResponse>;

public record GetSensorHistoryPeriodsResponse(IReadOnlyList<SensorPacketDto> Periods);

public static class GetSensorHistoryPeriodsErrors
{
  public static readonly Error InvalidRange = new("GetSensorHistoryPeriods.InvalidRange", "The start of the range must not be after the end of it.");
}

/// <summary>
/// Returns sensor packets averaged into fixed-width time periods (per sensor), so a caller charting a wide
/// range doesn't have to page through - and hold in memory - every raw packet the gateway ever recorded.
/// </summary>
public class GetSensorHistoryPeriodsHandler(KelvinContext context) : IHandler<GetSensorHistoryPeriodsRequest, GetSensorHistoryPeriodsResponse>
{
  // Regardless of the requested period width, this bounds how many small aggregate queries a single request
  // can trigger - a caller asking for 1-second periods over 30 days must not force millions of iterations.
  private const int MaxPeriods = 2_000;

  public async Task<Result<GetSensorHistoryPeriodsResponse>> HandleAsync(GetSensorHistoryPeriodsRequest request, CancellationToken ct = default)
  {
    if (request.From > request.To)
      return Result<GetSensorHistoryPeriodsResponse>.Failure(GetSensorHistoryPeriodsErrors.InvalidRange);

    var range = request.To - request.From;
    var periodSeconds = Math.Max(request.PeriodSeconds ?? GetDefaultPeriodSeconds(range), 1);
    var minPeriodSeconds = (int)Math.Ceiling(Math.Max(range.TotalSeconds, 1) / MaxPeriods);
    periodSeconds = Math.Max(periodSeconds, minPeriodSeconds);

    var sensorNames = await context.Sensors.AsNoTracking().ToDictionaryAsync(sensor => sensor.Id, sensor => sensor.Name, ct);

    var periods = new List<SensorPacketDto>();
    for (var periodStart = request.From; periodStart < request.To; periodStart = periodStart.AddSeconds(periodSeconds))
    {
      var periodEnd = periodStart.AddSeconds(periodSeconds);

      var query = context
        .SensorPackets.AsNoTracking()
        .Where(packet => packet.DeletedAt == null && packet.SensorId != null && packet.CreatedAt >= periodStart && packet.CreatedAt < periodEnd);

      if (request.SensorId is not null)
        query = query.Where(packet => packet.SensorId == request.SensorId);

      // Grouping/averaging happens in SQL - only one small aggregate row per sensor comes back per period,
      // never the raw packets that make up the average.
      var grouped = await query
        .GroupBy(packet => packet.SensorId)
        .Select(g => new
        {
          SensorId = g.Key,
          TemperatureC = g.Average(packet => packet.TemperatureC),
          HumidityPercentage = g.Average(packet => packet.HumidityPercentage),
          CO2LevelPpm = g.Average(packet => packet.CO2LevelPpm),
          SampleCount = g.Count(),
        })
        .ToListAsync(ct);

      var capturedPeriodStart = periodStart;
      periods.AddRange(
        grouped.Select(g => new SensorPacketDto(
          null,
          g.SensorId,
          g.SensorId.HasValue ? sensorNames.GetValueOrDefault(g.SensorId.Value) : null,
          capturedPeriodStart,
          g.TemperatureC,
          g.HumidityPercentage,
          (float)g.CO2LevelPpm,
          g.SampleCount
        ))
      );
    }

    return Result<GetSensorHistoryPeriodsResponse>.Success(new GetSensorHistoryPeriodsResponse(periods));
  }

  private static int GetDefaultPeriodSeconds(TimeSpan range)
  {
    if (range <= TimeSpan.FromHours(24))
      return 5 * 60;

    if (range <= TimeSpan.FromDays(7))
      return 30 * 60;

    return 2 * 60 * 60;
  }
}

public class GetSensorHistoryPeriodsEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet(
        "/api/sensors/readings/periods",
        async (
          IHandler<GetSensorHistoryPeriodsRequest, GetSensorHistoryPeriodsResponse> handler,
          CancellationToken ct,
          [FromQuery] DateTimeOffset from,
          [FromQuery] DateTimeOffset to,
          [FromQuery] Guid? sensorId = null,
          [FromQuery] int? periodSeconds = null
        ) =>
        {
          var result = await handler.HandleAsync(new GetSensorHistoryPeriodsRequest(from, to, sensorId, periodSeconds), ct);
          if (result.IsFailure)
          {
            if (result.Error == GetSensorHistoryPeriodsErrors.InvalidRange)
              return Results.BadRequest(result.Error);

            return Results.InternalServerError(result.Error);
          }

          return Results.Ok(result.Value);
        }
      )
      .WithName("GetSensorHistoryPeriods")
      .WithTags("Sensors");
  }
}

public class GetSensorHistoryPeriodsRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<GetSensorHistoryPeriodsRequest, GetSensorHistoryPeriodsResponse>, GetSensorHistoryPeriodsHandler>();
  }
}
