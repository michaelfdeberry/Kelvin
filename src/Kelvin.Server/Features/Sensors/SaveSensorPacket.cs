using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Sensors;

public record SaveSensorPacketRequest(SensorPacket SensorPacket) : IRequest;

public class SaveSensorPacketHandler(KelvinContext context, ISensorPacketChannel sensorPacketChannel) : IHandler<SaveSensorPacketRequest>
{
  public async Task<Result> HandleAsync(SaveSensorPacketRequest request, CancellationToken ct = default)
  {
    var sensor = await context.Sensors.FirstOrDefaultAsync(s => s.MacAddress == request.SensorPacket.MacAddress, ct);
    if (sensor is null)
    {
      sensor = new Sensor { MacAddress = request.SensorPacket.MacAddress, Enabled = true };
      context.Sensors.Add(sensor);
    }

    request.SensorPacket.SensorId = sensor.Id;
    context.SensorPackets.Add(request.SensorPacket);
    await context.SaveChangesAsync(ct);

    // always save the packet if one comes in because it contains the battery level,
    // but if it's not enabled don't send it to the channel for processing, because we don't want data from disabled sensors to be processed
    if (sensor.Enabled)
    {
      await sensorPacketChannel.WriteAsync(request.SensorPacket, ct);
    }

    return Result.Success();
  }
}

public class SaveSensorPacketFeatureRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<SaveSensorPacketRequest>, SaveSensorPacketHandler>();
  }
}
