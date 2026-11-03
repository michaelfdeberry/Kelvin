using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Notifications;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;

namespace Kelvin.Server.Services;

public class BalancingService(
  ILogger<BalancingService> logger,
  TimeProvider time,
  IDispatcher dispatcher,
  IEnvironmentReadingsChannel environmentReadingsChannel
) : BackgroundService, IEventHandler<ControlStateChangedEvent>
{
  private readonly Guid _subscriberId = Guid.NewGuid();

  private volatile bool _evaluationPending;

  private volatile TaskCompletionSource _heatCallEnded = NewSignal();

  private EnvironmentReading? _latestReading;

  private Guid? _activeNotificationId;

  private Task? _activeBalanceTask;

  private CancellationTokenSource? _activeBalanceCancellation;

  private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

  /// <summary>Called from a scoped publish, so it only flags the cycle and wakes the loop.</summary>
  public Task HandleAsync(ControlStateChangedEvent @event, CancellationToken cancellationToken = default)
  {
    var change = @event.Change;
    if (change is { Kind: ControlChangeKind.Call, State: ControlState.Dwell, PreviousState: ControlState.Heating })
    {
      _evaluationPending = true;
      _heatCallEnded.TrySetResult();
    }

    return Task.CompletedTask;
  }

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

        var heatCallEnded = _heatCallEnded.Task;
        var completed = _activeBalanceTask is null
          ? await Task.WhenAny(environmentReadingTask, heatCallEnded)
          : await Task.WhenAny(environmentReadingTask, heatCallEnded, _activeBalanceTask);

        if (completed == _activeBalanceTask)
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

        var readingArrived = completed == environmentReadingTask;
        if (readingArrived)
        {
          _latestReading = await environmentReadingTask;
          environmentReadingTask = null;
        }
        else
        {
          // Re-armed before evaluating so a cycle ending mid-evaluation wakes the next iteration.
          _heatCallEnded = NewSignal();
        }

        // A cycle that ends before the first reading is evaluated once one arrives.
        if (_latestReading is null)
          continue;

        var temperatures = _latestReading.Areas.Values.Select(a => a.TemperatureC).ToArray();
        var spreadC = temperatures.Length < 2 ? 0f : temperatures.Max() - temperatures.Min();

        if (_activeBalanceTask is not null)
        {
          if (readingArrived && spreadC <= thermostat.HeatingBalanceSatisfiedC)
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
