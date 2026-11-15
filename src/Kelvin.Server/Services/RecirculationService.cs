using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Notifications;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;

namespace Kelvin.Server.Services;

public class RecirculationService(
  ILogger<RecirculationService> logger,
  TimeProvider time,
  IDispatcher dispatcher,
  IFanArbiter fanArbiter,
  IEnvironmentReadingsChannel environmentReadingsChannel
) : BackgroundService, IEventHandler<ControlStateChangedEvent>
{
  private const int MaxCycles = 10;

  /// <summary>At or above this level any rest period is bypassed.</summary>
  private const float CriticalCO2Ppm = 2000;

  /// <summary>Rest period enforced after a cooling cycle ends.</summary>
  private static readonly TimeSpan CoolCallRestDuration = TimeSpan.FromMinutes(10);

  private readonly Guid _subscriberId = Guid.NewGuid();

  private volatile TaskCompletionSource _coolCallEnded = NewSignal();

  private Guid? _activeNotificationId;

  private Task? _activeRecirculationTask;

  private CancellationTokenSource? _activeRecirculationTaskCancellation;

  private DateTimeOffset? _offUntil;

  // Consecutive runs that hit the duration limit without reaching the satisfied level.
  private int _timedOutCycles;

  private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

  /// <summary>Called from a scoped publish, so it only wakes the loop.</summary>
  public Task HandleAsync(ControlStateChangedEvent @event, CancellationToken cancellationToken = default)
  {
    if (@event.Change is { Kind: ControlChangeKind.Call, State: ControlState.Dwell, PreviousState: ControlState.Cooling })
    {
      _coolCallEnded.TrySetResult();
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
        if (!thermostat.CO2RecirculationEnabled || thermostat.Mode == RunMode.Disabled)
        {
          if (_activeRecirculationTask is not null)
          {
            await EndRecirculation(thermostat, null, null, stoppingToken);
          }

          // A cooling call that ended while inactive must not start a rest once re-enabled.
          _coolCallEnded = NewSignal();
          _timedOutCycles = 0;

          await Task.Delay(TimeSpan.FromSeconds(1), time, stoppingToken);
          continue;
        }

        environmentReadingTask ??= environmentReadingsChannel.ReadAsync(_subscriberId, stoppingToken);

        var coolCallEnded = _coolCallEnded.Task;
        var completed = _activeRecirculationTask is null
          ? await Task.WhenAny(environmentReadingTask, coolCallEnded)
          : await Task.WhenAny(environmentReadingTask, coolCallEnded, _activeRecirculationTask);

        if (completed == coolCallEnded)
        {
          _coolCallEnded = NewSignal();

          if (_activeRecirculationTask is not null)
          {
            logger.LogInformation("Cooling cycle ended, stopping recirculation.");

            var notification = new SendNotificationRequest(
              "Recirculation stopped because the cooling cycle ended.",
              NotificationType.Information,
              Dismissible: true,
              Duration: 5000
            );

            await EndRecirculation(thermostat, notification, CoolCallRestDuration, stoppingToken);
          }
          else
          {
            // Never shortens a longer rest already in effect.
            var restUntil = time.GetUtcNow() + CoolCallRestDuration;
            if (_offUntil is not { } existing || existing < restUntil)
            {
              _offUntil = restUntil;
            }
          }

          continue;
        }

        if (completed == _activeRecirculationTask)
        {
          logger.LogInformation("Recirculation ran for {Duration}ms, stopping.", thermostat.CO2RecirculationDurationMs);

          var runDuration = TimeSpan.FromMilliseconds(thermostat.CO2RecirculationDurationMs).TotalMinutes;
          var notification = new SendNotificationRequest(
            $"Recirculation cycle stopped after running for {runDuration:0.#} minutes.",
            NotificationType.Information,
            Dismissible: true,
            Duration: 5000
          );

          var cycleDuration = TimeSpan.FromMilliseconds(thermostat.CO2RecirculationDurationMs);
          _timedOutCycles++;

          var restDuration = cycleDuration;
          if (_timedOutCycles >= MaxCycles)
          {
            // Rests as long as the whole series of on/off cycles took.
            restDuration = cycleDuration * 2 * MaxCycles;
            _timedOutCycles = 0;
            logger.LogInformation("Recirculation reached {MaxCycles} cycles, resting for {Rest}.", MaxCycles, restDuration);
          }

          await EndRecirculation(thermostat, notification, restDuration, stoppingToken);
          continue;
        }

        var reading = await environmentReadingTask;
        environmentReadingTask = null;

        // The highest area drives recirculation, since air circulation only evens levels out between rooms.
        var co2Level = !reading.Areas.IsEmpty ? reading.Areas.Max(a => a.Value.CO2LevelPpm) : reading.CO2LevelPpm;

        if (_activeRecirculationTask is not null && co2Level <= thermostat.CO2RecirculationSatisfiedPpm)
        {
          logger.LogInformation("{Co2Level} ppm reached, stopping recirculation.", co2Level);

          var notification = new SendNotificationRequest(
            $"Recirculation completed by achieving {co2Level}ppm of CO2.",
            NotificationType.Information,
            Dismissible: true,
            Duration: 5000
          );

          await EndRecirculation(thermostat, notification, null, stoppingToken);
          _timedOutCycles = 0;
          continue;
        }

        if (_activeRecirculationTask is null && co2Level <= thermostat.CO2RecirculationTriggerPpm)
        {
          // The demand went away, so the next one starts a fresh series of cycles.
          _timedOutCycles = 0;
        }

        if (_activeRecirculationTask is null && co2Level > thermostat.CO2RecirculationTriggerPpm)
        {
          if (co2Level < CriticalCO2Ppm && _offUntil is { } offUntil && time.GetUtcNow() < offUntil)
          {
            continue;
          }

          var cycleDuration = TimeSpan.FromMilliseconds(thermostat.CO2RecirculationDurationMs);

          logger.LogInformation("CO2 level of {CO2Level}ppm detected, enabling recirculation.", co2Level);

          var fanResult = await fanArbiter.AcquireAsync(FanOwner.Recirculation, thermostat, stoppingToken);
          fanResult.EnsureSuccess();

          _activeRecirculationTaskCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
          _activeRecirculationTask = Task.Delay(cycleDuration, time, _activeRecirculationTaskCancellation.Token);

          var notificationResult = await dispatcher.DispatchAsync<SendNotificationRequest, SendNotificationResponse>(
            new SendNotificationRequest(
              $"Recirculation activated because the CO2 level of {co2Level}ppm exceeded {thermostat.CO2RecirculationTriggerPpm}ppm.",
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

  private async Task EndRecirculation(
    Thermostat thermostat,
    SendNotificationRequest? notification,
    TimeSpan? offDuration,
    CancellationToken stoppingToken
  )
  {
    var result = await fanArbiter.ReleaseAsync(FanOwner.Recirculation, thermostat, stoppingToken);
    result.EnsureSuccess();

    _activeRecirculationTaskCancellation?.Cancel();
    _activeRecirculationTaskCancellation?.Dispose();
    _activeRecirculationTaskCancellation = null;
    _activeRecirculationTask = null;
    _offUntil = offDuration is { } off ? time.GetUtcNow() + off : null;

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
