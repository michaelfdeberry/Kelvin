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
  // Regardless of the requested period width, this bounds how many period groups a single request can
  // produce - a caller asking for 1-second periods over 30 days must not force millions of groups.
  private const int MaxPeriods = 2_000;

  public async Task<Result<GetSensorHistoryPeriodsResponse>> HandleAsync(GetSensorHistoryPeriodsRequest request, CancellationToken ct = default)
  {
    if (request.From > request.To)
      return Result<GetSensorHistoryPeriodsResponse>.Failure(GetSensorHistoryPeriodsErrors.InvalidRange);

    var range = request.To - request.From;
    var periodSeconds = Math.Max(request.PeriodSeconds ?? GetDefaultPeriodSeconds(range), 1);
    var minPeriodSeconds = (int)Math.Ceiling(Math.Max(range.TotalSeconds, 1) / MaxPeriods);
    periodSeconds = Math.Max(periodSeconds, minPeriodSeconds);
    var periodTicks = TimeSpan.FromSeconds(periodSeconds).Ticks;

    var sensorNames = await context.Sensors.AsNoTracking().ToDictionaryAsync(sensor => sensor.Id, sensor => sensor.Name, ct);

    var query = context
      .SensorPackets.AsNoTracking()
      .Where(packet => packet.DeletedAt == null && packet.SensorId != null && packet.CreatedAt >= request.From && packet.CreatedAt < request.To);

    if (request.SensorId is not null)
      query = query.Where(packet => packet.SensorId == request.SensorId);

    var projected = query.Select(packet => new
    {
      packet.SensorId,
      packet.CreatedAt,
      packet.TemperatureC,
      packet.HumidityPercentage,
      packet.CO2LevelPpm,
    });

    // A single query streamed row-by-row (not materialized into a list) and folded into running per-period
    // totals - one database round trip for the whole range instead of one aggregate query per period, and
    // only one row from the driver plus these small totals are ever held in memory at once.
    var totals = new Dictionary<(Guid SensorId, long PeriodIndex), PeriodTotals>();

    await foreach (var packet in projected.AsAsyncEnumerable().WithCancellation(ct))
    {
      if (packet.SensorId is not { } sensorId)
        continue;

      var periodIndex = (packet.CreatedAt - request.From).Ticks / periodTicks;
      var key = (sensorId, periodIndex);

      totals.TryGetValue(key, out var total);
      total.TemperatureTotal += packet.TemperatureC;
      total.HumidityTotal += packet.HumidityPercentage;
      total.CO2Total += packet.CO2LevelPpm;
      total.Count++;
      totals[key] = total;
    }

    var periods = totals
      .Select(entry => new SensorPacketDto(
        null,
        entry.Key.SensorId,
        sensorNames.GetValueOrDefault(entry.Key.SensorId),
        request.From + TimeSpan.FromTicks(entry.Key.PeriodIndex * periodTicks),
        (float)(entry.Value.TemperatureTotal / entry.Value.Count),
        (float)(entry.Value.HumidityTotal / entry.Value.Count),
        (float)(entry.Value.CO2Total / entry.Value.Count),
        entry.Value.Count
      ))
      .OrderBy(period => period.Timestamp)
      .ToList();

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

  private struct PeriodTotals
  {
    public double TemperatureTotal;
    public double HumidityTotal;
    public double CO2Total;
    public int Count;
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
