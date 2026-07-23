namespace Kelvin.Server.Services;

using System.Threading;
using System.Threading.Tasks;
using Kelvin.Server.Channels;
using Kelvin.Server.Models;
using Microsoft.Extensions.Hosting;

public class SensingService(ILogger<SensingService> logger, ISensorPacketChannel sensorPacketChannel, IEnvironmentChannel environmentChannel)
  : BackgroundService
{
  private readonly Guid subscriberId = Guid.NewGuid();

  private Environment? _environment;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        var sensorPacket = await sensorPacketChannel.ReadAsync(subscriberId, stoppingToken);
        if (sensorPacket is null)
          continue;

        if (sensorPacket.SensorId is null)
          continue;

        _environment ??= new();
        _environment.Timestamp = DateTimeOffset.UtcNow;
        _environment.Temperature = _environment.Areas.Values.Average(p => p.Temperature);
        _environment.Humidity = _environment.Areas.Values.Average(p => p.Humidity);
        _environment.CO2Level = _environment.Areas.Values.Average(p => p.CO2Level);
        _environment.Areas.AddOrUpdate(sensorPacket.SensorId.Value, sensorPacket, (_, __) => sensorPacket);

        await environmentChannel.WriteAsync(_environment, stoppingToken);
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error processing sensor packet");
      }
    }
  }
}
