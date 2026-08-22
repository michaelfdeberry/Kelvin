namespace Kelvin.Server.Services;

using System.Threading;
using System.Threading.Tasks;
using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Hubs;
using Kelvin.Server.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

public class SensingService(
  ILogger<SensingService> logger,
  ISensorPacketChannel sensorPacketChannel,
  IEnvironmentReadingsChannel environmentReadingChannel,
  IHubContext<EnvironmentReadingsHub, IEnvironmentReadingsClient> environmentReadingsHub,
  IDispatcher dispatcher,
  TimeProvider time
) : BackgroundService
{
  private readonly Guid subscriberId = Guid.NewGuid();

  private EnvironmentReading? _environment;

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

        var sensorsResponse = await dispatcher.DispatchAsync<GetSensorsRequest, GetSensorsResponse>(new GetSensorsRequest(), stoppingToken);
        sensorsResponse.EnsureSuccess();

        // just averaging everything for now, this may change later.
        _environment ??= new();
        _environment.Timestamp = time.GetUtcNow();
        _environment.Areas.AddOrUpdate(sensorPacket.SensorId.Value, sensorPacket, (_, _) => sensorPacket);

        var disabledSensors = sensorsResponse.Value!.Sensors.Where(s => !s.Enabled).Select(s => s.Id).ToHashSet();
        foreach (var disabledSensorId in disabledSensors)
        {
          _environment.Areas.TryRemove(disabledSensorId, out _);
        }

        _environment.TemperatureC = _environment.Areas.Values.Average(p => p.TemperatureC);
        _environment.HumidityPercentage = _environment.Areas.Values.Average(p => p.HumidityPercentage);
        _environment.CO2LevelPpm = (float)_environment.Areas.Values.Average(p => p.CO2LevelPpm);

        await environmentReadingChannel.WriteAsync(_environment, stoppingToken);
        await environmentReadingsHub.Clients.All.ReadingsUpdatedAsync(_environment);
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
