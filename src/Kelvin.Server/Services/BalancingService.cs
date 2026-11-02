using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Control;
using Kelvin.Server.Features.Notifications;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Models;

namespace Kelvin.Server.Services;

public class BalancingService(
  ILogger<BalancingService> logger,
  TimeProvider time,
  IDispatcher dispatcher,
  IEnvironmentReadingsChannel environmentReadingsChannel
) : BackgroundService
{
  private readonly Guid _subscriberId = Guid.NewGuid();

  private ControlState? _lastCallState;

  private bool _evaluationPending;

  private Guid? _activeNotificationId;

  private Task? _activeBalanceTask;

  private CancellationTokenSource? _activeBalanceCancellation;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    Task<EnvironmentReading>? environmentReadingTask = null;
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        var thermostatResult = await dispatcher.DispatchAsync<GetThermostatRequest, GetThermostatResponse>(new GetThermostatRequest(), stoppingToken);
        thermostatResult.EnsureSuccess();

        var controlResult = await dispatcher.DispatchAsync<GetControlStateRequest, GetControlStateResponse>(
          new GetControlStateRequest(),
          stoppingToken
        );
        controlResult.EnsureSuccess();

        var thermostat = thermostatResult.Value!.Thermostat;
        var callState = controlResult.Value!.CallState;

        if (_lastCallState == ControlState.Heating && callState == ControlState.Dwell)
        {
          _evaluationPending = true;
        }

        _lastCallState = callState;

        if (!thermostat.HeatingAutoBalanceEnabled || thermostat.Mode is not (RunMode.Heating or RunMode.Automatic))
        {
          _evaluationPending = false;

          if (_activeBalanceTask is not null)
          {
            await EndBalancing(thermostat, null, stoppingToken);
          }

          await Task.Delay(TimeSpan.FromSeconds(1), time, stoppingToken);
          continue;
        }

        environmentReadingTask ??= environmentReadingsChannel.ReadAsync(_subscriberId, stoppingToken);

        if (_activeBalanceTask is not null && await Task.WhenAny(_activeBalanceTask, environmentReadingTask) == _activeBalanceTask)
        {
          logger.LogInformation("Balancing ran for {Duration}ms, stopping.", thermostat.HeatingAutoBalanceDurationMs);

          var runDuration = TimeSpan.FromMilliseconds(thermostat.HeatingAutoBalanceDurationMs).TotalMinutes;
          var notification = new SendNotificationRequest(
            $"Heating balance cycle stopped after running for {runDuration:0.#} minutes.",
            NotificationType.Information,
            Dismissible: true,
            Duration: 5000
          );

          await EndBalancing(thermostat, notification, stoppingToken);
          continue;
        }

        // otherwise, process the reading
        var reading = await environmentReadingTask;
        environmentReadingTask = null;

        var temperatures = reading.Areas.Values.Select(a => a.TemperatureC).ToArray();
        var spreadC = temperatures.Length < 2 ? 0f : temperatures.Max() - temperatures.Min();

        if (_activeBalanceTask is not null)
        {
          if (spreadC <= thermostat.HeatingBalanceSatisfiedC)
          {
            logger.LogInformation("Temperature spread of {SpreadC}C reached, stopping balancing.", spreadC);

            var notification = new SendNotificationRequest(
              $"Heating balance completed with a {spreadC:0.#}°C temperature spread.",
              NotificationType.Information,
              Dismissible: true,
              Duration: 5000
            );

            await EndBalancing(thermostat, notification, stoppingToken);
          }

          _evaluationPending = false;
          continue;
        }

        if (_evaluationPending)
        {
          if (spreadC <= thermostat.HeatingBalanceTriggerC || thermostat.FanEnabled)
          {
            _evaluationPending = false;
          }
          else
          {
            logger.LogInformation("Heat cycle completed with a {SpreadC}C temperature spread, enabling balancing.", spreadC);

            var fanResult = await dispatcher.DispatchAsync(new UpdateThermostatRequest(thermostat.Mode, FanEnabled: true), stoppingToken);
            fanResult.EnsureSuccess();

            // cleared only on success so a failed enable is retried on the next reading
            _evaluationPending = false;

            _activeBalanceCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

            var duration = TimeSpan.FromMilliseconds(thermostat.HeatingAutoBalanceDurationMs);
            _activeBalanceTask = Task.Delay(duration, time, _activeBalanceCancellation.Token);

            var notificationResult = await dispatcher.DispatchAsync<SendNotificationRequest, SendNotificationResponse>(
              new SendNotificationRequest(
                $"Heating balance enabled because the temperature spread is {spreadC:0.#}°C.",
                NotificationType.Information,
                Dismissible: false
              ),
              stoppingToken
            );

            notificationResult.EnsureSuccess();
            _activeNotificationId = notificationResult.Value!.NotificationId;
          }
        }
      }
      catch (OperationCanceledException)
      {
        logger.LogInformation("BalancingService stopping due to cancellation.");
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "An error occurred while balancing heating.");
        await Task.Delay(TimeSpan.FromSeconds(1), time, stoppingToken);
      }
    }
  }

  private async Task EndBalancing(Thermostat thermostat, SendNotificationRequest? notification, CancellationToken stoppingToken)
  {
    var result = await dispatcher.DispatchAsync(new UpdateThermostatRequest(thermostat.Mode, FanEnabled: false), stoppingToken);
    result.EnsureSuccess();

    // State is only cleared once the fan is off, so a failed stop is retried next iteration.
    _activeBalanceCancellation?.Cancel();
    _activeBalanceCancellation?.Dispose();
    _activeBalanceCancellation = null;
    _activeBalanceTask = null;

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
