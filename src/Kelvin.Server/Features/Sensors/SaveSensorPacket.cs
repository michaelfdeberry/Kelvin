using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kelvin.Server.Features.Sensors;

public record SaveSensorPacketRequest(SensorPacket SensorPacket) : IRequest;

public class SaveSensorPacketHandler(KelvinContext context, ISensorPacketChannel sensorPacketChannel, IMemoryCache cache)
  : IHandler<SaveSensorPacketRequest>
{
  public async Task<Result> HandleAsync(SaveSensorPacketRequest request, CancellationToken ct = default)
  {
    var sensorPacket = request.SensorPacket;
    var sensor = await context.Sensors.FirstOrDefaultAsync(s => s.MacAddress == sensorPacket.MacAddress, ct);
    var clearCache = false;
    if (sensor is null)
    {
      sensor = new Sensor { MacAddress = sensorPacket.MacAddress, Enabled = true };
      context.Sensors.Add(sensor);
      clearCache = true;
    }

    // if it was deleted, but starts sending packets again restore it, but leave it disabled.
    if (sensor.DeletedAt is not null)
    {
      sensor.Enabled = false;
      sensor.DeletedAt = null;
    }

    sensorPacket.SensorId = sensor.Id;
    sensorPacket.TemperatureC += sensor.TemperatureCOffset;

    if (sensor.HasHumiditySensor)
    {
      sensorPacket.HumidityPercentage += sensor.HumidityPercentageOffset;
    }

    if (sensor.HasCO2Sensor)
    {
      sensorPacket.CO2LevelPpm = (ushort)(sensorPacket.CO2LevelPpm + sensor.CO2LevelPpmOffset);
    }

    context.SensorPackets.Add(sensorPacket);
    await context.SaveChangesAsync(ct);

    if (clearCache)
    {
      cache.Remove(SensorsCache.Key);
    }

    // Always save the packet if one comes in so the latest sensor state is retained,
    // but if it's not enabled don't send it to the channel for processing, because we don't want data from disabled sensors to be processed.
    if (sensor.Enabled)
    {
      await sensorPacketChannel.WriteAsync(sensorPacket, ct);
    }

    return Result.Success();
  }
}

public class SaveSensorPacketEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost(
        "/api/sensors/packets",
        async (SaveSensorPacketRequest request, IHandler<SaveSensorPacketRequest> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(request, ct);
          if (result.IsFailure)
          {
            return Results.InternalServerError(result.Error);
          }

          return Results.NoContent();
        }
      )
      .WithName("SaveSensorPacket")
      .WithTags("Sensors");
  }
}

public class SaveSensorPacketRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<SaveSensorPacketRequest>, SaveSensorPacketHandler>();
  }
}
