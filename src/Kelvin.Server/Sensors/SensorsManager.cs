using Kelvin.Server.Channels;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Sensors;

public interface ISensorsManager
{
  Task SaveSensorPacket(SensorPacket sensorPacket, CancellationToken stoppingToken);
}

public class SensorsManager(KelvinContext context, ISensorPacketChannel sensorPacketChannel) : ISensorsManager
{
  public async Task SaveSensorPacket(SensorPacket sensorPacket, CancellationToken stoppingToken)
  {
    var sensor = await context.Sensors.FirstOrDefaultAsync(s => s.MacAddress == sensorPacket.MacAddress, stoppingToken);
    if (sensor is null)
    {
      sensor = new Sensor { MacAddress = sensorPacket.MacAddress };
      context.Sensors.Add(sensor);
    }

    sensorPacket.SensorId = sensor.Id;
    context.SensorPackets.Add(sensorPacket);

    await context.SaveChangesAsync(stoppingToken);
    await sensorPacketChannel.WriteAsync(sensorPacket, stoppingToken);
  }
}
