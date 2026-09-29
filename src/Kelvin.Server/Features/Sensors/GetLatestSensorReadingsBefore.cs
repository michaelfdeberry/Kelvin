using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Sensors;

public record GetLatestSensorReadingsBeforeRequest(DateTimeOffset Before) : IRequest<GetLatestSensorReadingsBeforeResponse>;

public record GetLatestSensorReadingsBeforeResponse(IReadOnlyList<SensorPacketDto> Readings);

/// <summary>
/// Returns the most recent packet at or before a point in time, one per sensor - used to seed chart
/// continuity for a range that starts with no readings of its own yet. Unlike <see cref="GetLatestReadingsHandler" />,
/// this intentionally does not filter to currently-enabled sensors: a sensor removed since the reading was
/// taken should still show its own history rather than being dropped or renamed to a generic placeholder.
/// </summary>
public class GetLatestSensorReadingsBeforeHandler(KelvinContext context)
  : IHandler<GetLatestSensorReadingsBeforeRequest, GetLatestSensorReadingsBeforeResponse>
{
  public async Task<Result<GetLatestSensorReadingsBeforeResponse>> HandleAsync(
    GetLatestSensorReadingsBeforeRequest request,
    CancellationToken ct = default
  )
  {
    var latestPerSensor = await context
      .SensorPackets.AsNoTracking()
      .Where(packet => packet.DeletedAt == null && packet.SensorId != null && packet.CreatedAt <= request.Before)
      .GroupBy(packet => packet.SensorId)
      .Select(g => g.OrderByDescending(packet => packet.CreatedAt).First())
      .ToListAsync(ct);

    var sensorIds = latestPerSensor.Select(packet => packet.SensorId!.Value).ToList();
    var sensorNames = await context
      .Sensors.AsNoTracking()
      .Where(sensor => sensorIds.Contains(sensor.Id))
      .ToDictionaryAsync(sensor => sensor.Id, sensor => sensor.Name, ct);

    var readings = latestPerSensor
      .Select(packet => new SensorPacketDto(
        packet.Id,
        packet.SensorId,
        packet.SensorId.HasValue ? sensorNames.GetValueOrDefault(packet.SensorId.Value) : null,
        packet.CreatedAt,
        packet.TemperatureC,
        packet.HumidityPercentage,
        packet.CO2LevelPpm,
        1
      ))
      .ToList();

    return Result<GetLatestSensorReadingsBeforeResponse>.Success(new GetLatestSensorReadingsBeforeResponse(readings));
  }
}

public class GetLatestSensorReadingsBeforeEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet(
        "/api/sensors/readings/latest-before",
        async (
          IHandler<GetLatestSensorReadingsBeforeRequest, GetLatestSensorReadingsBeforeResponse> handler,
          CancellationToken ct,
          [FromQuery] DateTimeOffset before
        ) =>
        {
          var result = await handler.HandleAsync(new GetLatestSensorReadingsBeforeRequest(before), ct);
          return Results.Ok(result.Value);
        }
      )
      .WithName("GetLatestSensorReadingsBefore")
      .WithTags("Sensors");
  }
}

public class GetLatestSensorReadingsBeforeRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<GetLatestSensorReadingsBeforeRequest, GetLatestSensorReadingsBeforeResponse>, GetLatestSensorReadingsBeforeHandler>();
  }
}
