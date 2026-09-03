namespace Kelvin.Server.Services;

using System.Collections.Concurrent;
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
  IControlChannel controlChannel,
  IHubContext<NotificationsHub, INotificationsClient> notificationHub,
  IHubContext<EnvironmentReadingsHub, IEnvironmentReadingsClient> environmentReadingsHub,
  IDispatcher dispatcher,
  TimeProvider time
) : BackgroundService
{
  // The sensors poll every 30 seconds and push updates only if there are changes greater than the margin of error of the sensor (mostly).
  // Otherwise it will check-in every 5 minutes to ensure the sensor is still alive and sending data.
  private const int SENSOR_HEARTBEAT_INTERVAL_MS = 5 * 60 * 1000; // 5 minutes

  // If we haven't received a packet from a sensor in 15 minutes, we consider it offline and remove it from the environment reading.
  // For now, this will be 3 times the check-in interval.
  private const int SENSOR_TIMEOUT_MS = 3 * SENSOR_HEARTBEAT_INTERVAL_MS;

  private readonly Guid subscriberId = Guid.NewGuid();

  private EnvironmentReading? _environment;

  private readonly ConcurrentDictionary<Guid, DateTimeOffset> _enabledSensorsWithoutReadings = new();

  private readonly SemaphoreSlim _environmentLock = new(1, 1);

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var monitorSensorHealthTask = MonitorSensorHealthAsync(stoppingToken);
    var listenForSensorPacketsTask = ListenForSensorPacketsAsync(stoppingToken);

    await Task.WhenAll(monitorSensorHealthTask, listenForSensorPacketsTask);
  }

  private async Task MonitorSensorHealthAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await Task.Delay(TimeSpan.FromMilliseconds(SENSOR_HEARTBEAT_INTERVAL_MS), time, stoppingToken);

        var sensorsResponse = await dispatcher.DispatchAsync<GetSensorsRequest, GetSensorsResponse>(new GetSensorsRequest(), stoppingToken);
        sensorsResponse.EnsureSuccess();

        var sensors = sensorsResponse.Value!.Sensors;

        await _environmentLock.WaitAsync(stoppingToken);
        var requiresUpdate = false;
        try
        {
          _environment ??= new();
          requiresUpdate = await PruneEnvironmentReadingAsync(sensors, stoppingToken);
        }
        finally
        {
          _environmentLock.Release();
        }

        if (requiresUpdate)
        {
          await UpdateReadingsAsync(sensors, stoppingToken);
        }
      }
      catch (OperationCanceledException)
      {
        logger.LogInformation("SensingService is stopping due to cancellation.");
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "An error occurred in SensingService while monitoring sensor health.");
      }
    }
  }

  private async Task ListenForSensorPacketsAsync(CancellationToken stoppingToken)
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

        var sensors = sensorsResponse.Value!.Sensors;
        if (sensors.Any(s => s.Id == sensorPacket.SensorId && !s.Enabled))
        {
          logger.LogWarning("Received a sensor packet from a disabled sensor (ID: {SensorId}). Ignoring.", sensorPacket.SensorId);
          continue;
        }

        await _environmentLock.WaitAsync(stoppingToken);
        try
        {
          _environment ??= new();
          _environment.Areas.AddOrUpdate(sensorPacket.SensorId.Value, sensorPacket, (_, _) => sensorPacket);
          _enabledSensorsWithoutReadings.TryRemove(sensorPacket.SensorId.Value, out _);

          await PruneEnvironmentReadingAsync(sensors, stoppingToken);
        }
        finally
        {
          _environmentLock.Release();
        }

        await UpdateReadingsAsync(sensors, stoppingToken);
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

  private async Task UpdateReadingsAsync(IEnumerable<SensorResponse> sensors, CancellationToken stoppingToken)
  {
    if (_environment?.Areas.IsEmpty ?? true)
      return;

    _environment.Timestamp = time.GetUtcNow();
    _environment.TemperatureC = _environment.Areas.Values.Average(p => p.TemperatureC);
    _environment.HumidityPercentage = _environment.Areas.Values.Average(p => p.HumidityPercentage);
    _environment.CO2LevelPpm = (float)_environment.Areas.Values.Average(p => p.CO2LevelPpm);

    await environmentReadingChannel.WriteAsync(_environment, stoppingToken);
    await environmentReadingsHub.Clients.All.ReadingsUpdated(_environment);
  }

  private async Task<bool> PruneEnvironmentReadingAsync(IEnumerable<SensorResponse> sensors, CancellationToken stoppingToken)
  {
    if (_environment is null)
      return false;

    var disabledSensors = sensors.Where(s => !s.Enabled).Select(s => s.Id).ToHashSet();
    var removedDisabled = disabledSensors.Count(id => _environment.Areas.TryRemove(id, out _));
    if (removedDisabled > 0)
      logger.LogInformation("Removed {Count} disabled sensors from environment reading.", removedDisabled);

    var now = time.GetUtcNow();

    // if there are enabled sensors without readings, cache them so we can check if they come back online later
    var sensorsWithoutReadings = sensors.Where(x => x.Enabled && !_environment.Areas.ContainsKey(x.Id)).Select(x => x.Id).ToList();
    foreach (var sensorId in sensorsWithoutReadings)
    {
      _enabledSensorsWithoutReadings.TryAdd(sensorId, now);
    }

    var timedOutSensors = _environment.Areas.Where(p => (now - p.Value.CreatedAt).TotalMilliseconds > SENSOR_TIMEOUT_MS).Select(p => p.Key).ToList();
    var sensorsWithoutUpdates = _enabledSensorsWithoutReadings
      .Where(p => (now - p.Value).TotalMilliseconds > SENSOR_TIMEOUT_MS)
      .Select(p => p.Key)
      .ToList();

    var sensorsToCleanup = timedOutSensors.Union(sensorsWithoutUpdates).ToList();
    if (sensorsToCleanup.Count > 0)
    {
      foreach (var sensorId in sensorsToCleanup)
      {
        _enabledSensorsWithoutReadings.TryRemove(sensorId, out _);
        _environment.Areas.TryRemove(sensorId, out _);
      }
      logger.LogInformation("Removed {Count} timed out sensors from environment reading.", sensorsToCleanup.Count);

      if (_environment.Areas.IsEmpty)
      {
        logger.LogCritical("All sensors have timed out, relinquishing control to the fail-safe thermostat.");
        await controlChannel.WriteAsync(new ControlMessage(ControlState.Disable, Reason: "All sensors have timed out."), stoppingToken);

        var notification = new Notification("All sensors are offline, entering fail-safe mode.", NotificationType.Error, Banner: true);
        await notificationHub.Clients.All.Notify(notification);

        _environment = new();
      }
    }

    return removedDisabled > 0 || sensorsToCleanup.Count > 0;
  }
}
