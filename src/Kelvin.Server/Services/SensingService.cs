namespace Kelvin.Server.Services;

using System.Threading;
using System.Threading.Tasks;
using Kelvin.Server.Channels;
using Kelvin.Server.Models;
using Microsoft.Extensions.Hosting;

public class SensingService(
  ILogger<SensingService> logger,
  ISensorPacketChannel sensorPacketChannel,
  IEnvironmentChannel environmentChannel,
  TimeProvider time
) : BackgroundService
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

        // just averaging everything for now, this may change later.
        _environment ??= new();
        _environment.Timestamp = time.GetUtcNow();
        _environment.TemperatureC = _environment.Areas.Values.Average(p => p.TemperatureC);
        _environment.HumidityPercentage = _environment.Areas.Values.Average(p => p.HumidityPercentage);
        _environment.CO2LevelPpm = _environment.Areas.Values.Average(p => p.CO2LevelPpm);
        _environment.Areas.AddOrUpdate(sensorPacket.SensorId.Value, sensorPacket, (_, _) => sensorPacket);

        await environmentChannel.WriteAsync(_environment, stoppingToken);
      }
      catch (OperationCanceledException)
      {
        logger.LogInformation("SensingService is stopping due to cancellation.");
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "An error occurred in SensingService while processing sensor packets.");
      }
    }
  }
}
