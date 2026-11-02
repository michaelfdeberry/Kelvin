using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Notifications;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Models;

namespace Kelvin.Server.Services;

public class RecirculationService(
  ILogger<RecirculationService> logger,
  TimeProvider time,
  IDispatcher dispatcher,
  IEnvironmentReadingsChannel environmentReadingsChannel
) : BackgroundService
{
  private readonly Guid _subscriberId = Guid.NewGuid();

  private Guid? _activeNotificationId;

  private Task? _activeRecirculationTask;

  private CancellationTokenSource? _activeRecirculationTaskCancellation;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    Task<EnvironmentReading>? environmentReadingTask = null;
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        var thermostatResult = await dispatcher.DispatchAsync<GetThermostatRequest, GetThermostatResponse>(new GetThermostatRequest(), stoppingToken);
        thermostatResult.EnsureSuccess();

        var thermostat = thermostatResult.Value!.Thermostat;
        if (!thermostat.CO2RecirculationEnabled)
        {
          if (_activeRecirculationTask is not null)
          {
            await EndRecirculation(thermostat, null, stoppingToken);
          }

          await Task.Delay(TimeSpan.FromSeconds(1), time, stoppingToken);
          continue;
        }

        environmentReadingTask ??= environmentReadingsChannel.ReadAsync(_subscriberId, stoppingToken);

        if (_activeRecirculationTask is not null && await Task.WhenAny(_activeRecirculationTask, environmentReadingTask) == _activeRecirculationTask)
        {
          logger.LogInformation("Recirculation ran for {Duration}ms, stopping.", thermostat.CO2RecirculationDurationMs);

          var runDuration = TimeSpan.FromMilliseconds(thermostat.CO2RecirculationDurationMs).TotalMinutes;
          var notification = new SendNotificationRequest(
            $"CO2 Recirculation cycle stopped after running for {runDuration:0.#} minutes.",
            NotificationType.Information,
            Dismissible: true,
            Duration: 5000
          );

          await EndRecirculation(thermostat, notification, stoppingToken);
          continue;
        }

        // otherwise, process the reading
        var reading = await environmentReadingTask;
        environmentReadingTask = null;

        var co2Level = thermostat.CO2RecirculationMode switch
        {
          CO2RecirculationMode.Average => reading.CO2LevelPpm,
          CO2RecirculationMode.Peak => reading.Areas.Any() ? reading.Areas.Max(a => a.Value.CO2LevelPpm) : reading.CO2LevelPpm,
          _ => throw new InvalidOperationException($"Unknown recirculation mode {thermostat.CO2RecirculationMode}."),
        };

        if (_activeRecirculationTask is not null && co2Level <= thermostat.CO2RecirculationSatisfiedPpm)
        {
          logger.LogInformation("{Co2Level} ppm reached, stopping recirculation.", co2Level);

          var notification = new SendNotificationRequest(
            $"CO2 Recirculation completed by achieving {co2Level}ppm.",
            NotificationType.Information,
            Dismissible: true,
            Duration: 5000
          );

          await EndRecirculation(thermostat, notification, stoppingToken);
          continue;
        }

        if (_activeRecirculationTask is null && co2Level > thermostat.CO2RecirculationTriggerPpm)
        {
          logger.LogInformation("{Mode} CO2 level of {CO2Level}ppm detected, enabling recirculation.", thermostat.CO2RecirculationMode, co2Level);

          var fanResult = await dispatcher.DispatchAsync(new UpdateThermostatRequest(thermostat.Mode, FanEnabled: true), stoppingToken);
          fanResult.EnsureSuccess();

          _activeRecirculationTaskCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

          var duration = TimeSpan.FromMilliseconds(thermostat.CO2RecirculationDurationMs);
          _activeRecirculationTask = Task.Delay(duration, time, _activeRecirculationTaskCancellation.Token);

          var notificationResult = await dispatcher.DispatchAsync<SendNotificationRequest, SendNotificationResponse>(
            new SendNotificationRequest(
              $"CO2 Recirculation enabled because a {thermostat.CO2RecirculationMode} level of {co2Level} detected.",
              NotificationType.Warning,
              Dismissible: false
            ),
            stoppingToken
          );

          notificationResult.EnsureSuccess();
          _activeNotificationId = notificationResult.Value!.NotificationId;
        }
      }
      catch (OperationCanceledException)
      {
        logger.LogInformation("RecirculationService stopping due to cancellation.");
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "An error occurred while monitoring Co2 levels.");
        await Task.Delay(TimeSpan.FromSeconds(1), time, stoppingToken);
      }
    }
  }

  private async Task EndRecirculation(Thermostat thermostat, SendNotificationRequest? notification, CancellationToken stoppingToken)
  {
    var result = await dispatcher.DispatchAsync(new UpdateThermostatRequest(thermostat.Mode, FanEnabled: false), stoppingToken);
    result.EnsureSuccess();

    _activeRecirculationTaskCancellation?.Cancel();
    _activeRecirculationTaskCancellation?.Dispose();
    _activeRecirculationTaskCancellation = null;
    _activeRecirculationTask = null;

    if (_activeNotificationId.HasValue)
    {
      var notificationId = _activeNotificationId.Value;
      _activeNotificationId = null;
      await dispatcher.DispatchAsync(new DismissNotificationRequest(notificationId), stoppingToken);
    }

    if (notification is not null)
    {
      await dispatcher.DispatchAsync<SendNotificationRequest, SendNotificationResponse>(notification, stoppingToken);
    }
  }
}
