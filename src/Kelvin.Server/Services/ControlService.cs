using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Control;
using Kelvin.Server.Features.Gateways;
using Kelvin.Server.Hubs;
using Kelvin.Server.Models;
using Microsoft.AspNetCore.SignalR;

namespace Kelvin.Server.Services;

/// <summary>
/// Consumes control messages and actuates the HVAC relays, applying the hardware safety guards.
/// </summary>
/// <remarks>
/// State is tracked by concern rather than as one combined state, so the minimum on/off duration guards in
/// <see cref="EvaluateCall" /> only ever see the states they apply to: control ownership is a flag driven
/// exclusively by <see cref="ControlState.Enable" /> and <see cref="ControlState.Disable" />, the fan is stateless
/// and actuated directly, and only an <see cref="HvacCall" /> reaches the guards.
/// </remarks>
public class ControlService(
  ILogger<ControlService> logger,
  IControlChannel controlChannel,
  IDispatcher dispatcher,
  IRelayController relays,
  TimeProvider time,
  IHostApplicationLifetime lifetime,
  IHubContext<ControlHub, IControlClient> hub
) : BackgroundService
{
  private static readonly Guid subscriberId = Guid.NewGuid();

  // Hardware safety configurations
  private const int DEFAULT_MIN_OFF_DURATION_MINUTES = 5;
  private const int DEFAULT_MIN_ON_DURATION_MINUTES = 3;
  private const int DEFAULT_MIN_MODE_SWITCH_DURATION_MINUTES = 15;

  private const int ERROR_BACKOFF_SECONDS = 5;

  // State tracking
  private bool _controlEnabled;
  private HvacCall _currentCall = HvacCall.Dwell;
  private bool _fanOn;
  private DateTimeOffset _lastCallChangeAt = DateTimeOffset.MinValue;
  private DateTimeOffset? _lastControlChangeAt;
  private DateTimeOffset? _lastFanChangeAt;
  private CancellationTokenSource? _pendingDwellCts;
  private Task? _pendingDwellTask;
  private DateTimeOffset _lastCoolingEndedAt = DateTimeOffset.MinValue;
  private DateTimeOffset _lastHeatingEndedAt = DateTimeOffset.MinValue;

  // Change recording. Records are queued by the state machine and broadcast/persisted only after the actuation is
  // complete, so a slow or failing hub or database can never delay a relay or take the control loop down with it.
  private readonly List<ControlStateChange> _pendingChanges = [];
  private ControlMessage? _currentContext;

  public override async Task StartAsync(CancellationToken cancellationToken)
  {
    relays.Initialize();

    await RestoreCallClockAsync(cancellationToken);
    await RecordStartupEventAsync(cancellationToken);
    await base.StartAsync(cancellationToken);
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    // The read is kept outstanding across iterations. A scheduled dwell transition can come due while a message is
    // already in flight, and cancelling the read to apply it would discard a message the channel had handed over.
    Task<ControlMessage>? outstandingRead = null;

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        outstandingRead ??= controlChannel.ReadAsync(subscriberId, stoppingToken);

        // A scheduled dwell never delays anything else: whichever of the two completes first is dealt with in this
        // iteration, and a message still in flight is picked up by the next one.
        if (_pendingDwellTask is not null && await Task.WhenAny(outstandingRead, _pendingDwellTask) == _pendingDwellTask)
        {
          CancelPendingDwellTransition();
          // The transition is driven by the clock rather than by a message, so there is no producer context.
          _currentContext = null;
          TransitionToCall(HvacCall.Dwell, "the minimum on-time elapsed");
          await FlushChangesAsync(stoppingToken);
          continue;
        }

        var read = outstandingRead;
        outstandingRead = null;

        var controlMessage = await read;
        if (controlMessage is null)
          continue;

        // Fetched only once a message has arrived: the read can stay outstanding for hours, and the guards and pin
        // configuration must reflect the settings as they are now, not as they were when the wait began.
        var gatewayResult = await dispatcher.DispatchAsync<GetGatewayRequest, GetGatewayResponse>(new(), stoppingToken);
        gatewayResult.EnsureSuccess();

        var gateway = gatewayResult.Value!;

        relays.Configure(gateway);

        _currentContext = controlMessage;
        Handle(gateway, _currentContext.State);
        await FlushChangesAsync(stoppingToken);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        logger.LogInformation("ControlService is stopping due to cancellation.");
        break;
      }
      catch (GpioUnavailableException ex)
      {
        await RecordFaultEventAsync("GPIO became unusable", stoppingToken);
        // never continue the state machine on unusable hardware; fail loudly instead of pretending to actuate
        logger.LogCritical(ex, "GPIO became unusable. Stopping the application so the failure is not silently ignored.");
        lifetime.StopApplication();
        return;
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "An error occurred in ControlService while processing control commands.");
        // Keeps a persistent failure (e.g. an unreachable database) from spinning the loop.
        await BackoffAfterErrorAsync(stoppingToken);
      }
    }
  }

  private async Task BackoffAfterErrorAsync(CancellationToken stoppingToken)
  {
    try
    {
      await Task.Delay(TimeSpan.FromSeconds(ERROR_BACKOFF_SECONDS), time, stoppingToken);
    }
    catch (OperationCanceledException)
    {
      // Shutdown during the backoff; the loop condition handles the exit.
    }
  }

  /// <summary>Routes a requested control state to the relays, applying the hardware safety guards.</summary>
  private void Handle(GetGatewayResponse gateway, ControlState requested)
  {
    // Taking control back from the legacy thermostat is the only way out of Disable, so it is handled before the
    // guard below.
    if (requested == ControlState.Enable)
    {
      EnableControl();
      return;
    }

    // Disabling is either on demand by the user or due to a failure, so control is reverted to the legacy thermostat
    // immediately regardless of how long the current call has been running.
    if (requested == ControlState.Disable)
    {
      DisableControl(gateway, "Disable was requested");
      return;
    }

    // While control is reverted the legacy thermostat owns the equipment, so nothing else is actuated - including the
    // fan, whose relay is downstream of the control relay.
    if (!_controlEnabled)
    {
      logger.LogInformation("Ignoring {RequestedState} request while control is reverted to the legacy thermostat.", requested);
      return;
    }

    // The fan can be toggled independently of the current call and doesn't need to be tracked.
    if (requested is ControlState.FanOn or ControlState.FanOff)
    {
      ToggleFan(requested);
      return;
    }

    if (!TryGetCall(requested, out var call))
    {
      logger.LogWarning("Unhandled ControlState: {RequestedState}", requested);
      return;
    }

    EvaluateCall(gateway, call);
  }

  /// <summary>
  /// Applies the minimum on/off duration guards to a requested call. Control is known to be enabled here, and
  /// <paramref name="call" /> is the only state the guards apply to.
  /// </summary>
  private void EvaluateCall(GetGatewayResponse gateway, HvacCall call)
  {
    // The durations were going to be configurable, but that's probably not going to happen
    // TODO: remove the properties from the Gateway since they are no longer used.
    var minOffDuration = TimeSpan.FromMinutes(DEFAULT_MIN_OFF_DURATION_MINUTES);
    var minOnDuration = TimeSpan.FromMinutes(DEFAULT_MIN_ON_DURATION_MINUTES);
    var minTransitionDuration = TimeSpan.FromMinutes(DEFAULT_MIN_MODE_SWITCH_DURATION_MINUTES);

    var timeInCurrentCall = time.GetUtcNow() - _lastCallChangeAt;

    if (call == HvacCall.Dwell)
    {
      // already in Dwell, so make sure the relays are released.
      if (_currentCall == HvacCall.Dwell)
      {
        relays.EnableDwell();
        return;
      }

      // already waiting to transition to Dwell
      if (_pendingDwellTask is not null)
        return;

      // leaving an active Heating/Cooling call for Dwell is subject to the Minimum On-Time guard.
      if (timeInCurrentCall < minOnDuration)
      {
        var remaining = minOnDuration - timeInCurrentCall;
        logger.LogInformation(
          "Requested Dwell, but Minimum On-Time ({RequiredMinutes}m) has not elapsed. Transitioning to Dwell in {RemainingSeconds}s unless another call for {CurrentCall} arrives first.",
          minOnDuration.TotalMinutes,
          remaining.TotalSeconds,
          _currentCall
        );
        ScheduleDwellTransition(remaining);
        return;
      }

      // No reason is supplied: the requested state is already the whole story, so the producer's explanation of
      // why it asked is the more useful thing to record.
      TransitionToCall(HvacCall.Dwell, null);
      return;
    }

    if (_currentCall == HvacCall.Dwell)
    {
      var now = time.GetUtcNow();

      // if leaving dwell the min off time might need to be adjusted if the last active call was the opposite mode.
      if (call == HvacCall.Heating && _lastCoolingEndedAt != DateTimeOffset.MinValue && now - _lastCoolingEndedAt < minTransitionDuration)
      {
        logger.LogInformation(
          "Requested {RequestedCall}, but the last Cooling call ended {ElapsedSeconds}s ago, which is less than the Minimum Mode Switch Duration ({RequiredMinutes}m).",
          call,
          (now - _lastCoolingEndedAt).TotalSeconds,
          minTransitionDuration.TotalMinutes
        );

        // block activation
        return;
      }
      else if (call == HvacCall.Cooling && _lastHeatingEndedAt != DateTimeOffset.MinValue && now - _lastHeatingEndedAt < minTransitionDuration)
      {
        logger.LogInformation(
          "Requested {RequestedCall}, but the last Heating call ended {ElapsedSeconds}s ago, which is less than the Minimum Mode Switch Duration ({RequiredMinutes}m).",
          call,
          (now - _lastHeatingEndedAt).TotalSeconds,
          minTransitionDuration.TotalMinutes
        );

        // block activation
        return;
      }
      else if (timeInCurrentCall < minOffDuration)
      {
        logger.LogInformation(
          "Requested {RequestedCall}, but Minimum Off-Time ({RequiredMinutes}m) has not elapsed. Blocked for {RemainingSeconds}s.",
          call,
          minOffDuration.TotalMinutes,
          (minOffDuration - timeInCurrentCall).TotalSeconds
        );

        // block activation
        return;
      }

      TransitionToCall(call, null);
    }
    else if (_currentCall == call)
    {
      if (_pendingDwellTask is not null)
      {
        logger.LogInformation("Received another call for {RequestedCall} while waiting to transition to Dwell; cancelling the wait.", call);
        CancelPendingDwellTransition();
      }
    }
    else
    {
      // Heating and Cooling are the only two active calls reachable here, so this guards against a direct
      // Heating<->Cooling switch.
      // Maybe there is some concern for this in the automatic mode, but it probably wouldn't make sense to configure it that tightly anyway.
      // There will need to be some validation against this being allowed to be configured in this way.
      //
      // This was initially being treated as a critical error because it shouldn't be possible to reach this point without a bug in the code.
      // It previously reverted control because going from heating to cooling may crack the heat exchanger and going from cooling to heating may
      // damage the evaporator coil.
      //
      // My assumptions are that normal thermostats are dumb and the furnace control board is designed to handle this,
      // I wouldn't bet my furnace on it though.
      //
      // Update - I looked into this more, it's a true concern and from what I've found it's not handled by the control board at all.
      // The biggest concern is stressing the heat exchanger and causing it to crack, from what I can find the evaporator coil would have no issues
      // with temperature swings.

      // It seems the recommended approach is to have a minimum off time, which is already implemented. However, that's the typical
      // 5 minutes between states. That might be fine, but it's at the low end of the recommended time. From what I can find, the recommendation is between
      // 5 and 15 minutes. I'd prefer to be on the high end of that.
      //
      // I'd rather not support a direct switch at all. However, with the initial implementation the min off time will be at the low end of the suggested
      // dwell time, and this could still be an indirect issue by going heating->dwell->cooling or cooling->dwell->heating.
      // Which means it needed to be accounted for anyway, so there is no point in relinquishing control and letting the legacy thermostat handle it.

      // Just having this transition to Dwell should be enough. Because, if it's in heating and then there is a call for cooling
      // changing to dwell would force the min off time. Since that code was updated to account for the last active call, and not just the dwell time,
      // it is safe to go heating->dwell->cooling. If cooling is really needed another call will likely come in before
      // the min off time elapses.

      // That eliminates the safety concern, the only other concern would looping through states:
      // heating->dwell->cooling->dwell->heating->dwell->cooling...
      // However, it's unlikely someone would manually cycle their system in such a way, and the automatic mode doesn't allow
      // for configuring the thermostat where a switch like that would be realistically possible with the forecast lockouts and the
      // required 2x hysteresis gap.
      // Leaving all these notes since this might be the most critical safety concern in the system.
      _currentContext = (_currentContext ?? new(ControlState.Dwell)) with
      {
        State = ControlState.Dwell,
        Reason = $"Attempted to switch directly from {_currentCall} to {call} without an intermediate idle state. Transitioning to Dwell first.",
      };

      logger.LogWarning(
        "Attempted to switch directly from {CurrentCall} to {RequestedCall} without an intermediate idle state. Transitioning to Dwell first.",
        _currentCall,
        call
      );
      EvaluateCall(gateway, HvacCall.Dwell);
    }
  }

  private void EnableControl()
  {
    // Re-asserted on every thermostat cycle, so taking control must not disturb an active call or restart the
    // minimum on/off clocks.
    if (_controlEnabled)
      return;

    logger.LogInformation("Taking control from the Legacy Thermostat (Control - relay energized).");
    _controlEnabled = true;
    // EnableControl releases the heating, cooling and fan relays, so the system is idle after taking over. The
    // minimum off clock is deliberately left where reverting control set it rather than restarted here.
    _currentCall = HvacCall.Dwell;
    relays.EnableControl();

    RecordChange(ControlChangeKind.Control, ControlState.Enable, ControlState.Disable, _lastControlChangeAt, "control was requested");
    _lastControlChangeAt = time.GetUtcNow();
    RecordFanReleasedByControlRelay();
  }

  private void DisableControl(GetGatewayResponse gateway, string reason)
  {
    if (_pendingDwellTask is not null)
    {
      logger.LogInformation("Reverting control while waiting to transition to Dwell; cancelling the wait.");
      CancelPendingDwellTransition();
    }

    // Re-asserted on every thermostat cycle while disabled, so only the transition itself is logged and clocked.
    if (!_controlEnabled)
      return;

    var minOnDuration = TimeSpan.FromMinutes(gateway.MinimumOnDurationMinutes ?? DEFAULT_MIN_ON_DURATION_MINUTES);
    if (_currentCall != HvacCall.Dwell && time.GetUtcNow() - _lastCallChangeAt < minOnDuration)
      logger.LogWarning(
        "Reverting control immediately even though Minimum On-Time ({RequiredMinutes}m) has not elapsed.",
        minOnDuration.TotalMinutes
      );

    var previousCall = _currentCall;
    var previousCallSince = _lastCallChangeAt == DateTimeOffset.MinValue ? (DateTimeOffset?)null : _lastCallChangeAt;

    logger.LogInformation("Deactivating HVAC relays, reverting control to Failsafe NC (Legacy Thermostat) because {Reason}.", reason);
    _controlEnabled = false;
    _currentCall = HvacCall.Dwell;
    _lastCallChangeAt = time.GetUtcNow();
    relays.DisableControl();

    RecordChange(ControlChangeKind.Control, ControlState.Disable, ControlState.Enable, _lastControlChangeAt, reason);
    _lastControlChangeAt = time.GetUtcNow();

    if (previousCall == HvacCall.Cooling)
      _lastCoolingEndedAt = time.GetUtcNow();

    if (previousCall == HvacCall.Heating)
      _lastHeatingEndedAt = time.GetUtcNow();

    // The control relay released the active call with it, so the call timeline must not show it still running.
    if (previousCall != HvacCall.Dwell)
      RecordChange(
        ControlChangeKind.Call,
        ControlState.Dwell,
        ToControlState(previousCall),
        previousCallSince,
        "the control relay released the call"
      );

    RecordFanReleasedByControlRelay();
  }

  /// <summary>
  /// Records the fan going off when taking or handing back control released its relay, so the fan timeline does
  /// not show it still running.
  /// </summary>
  private void RecordFanReleasedByControlRelay()
  {
    if (!_fanOn)
      return;

    _fanOn = false;
    RecordChange(ControlChangeKind.Fan, ControlState.FanOff, ControlState.FanOn, _lastFanChangeAt, "the control relay released the fan");
    _lastFanChangeAt = time.GetUtcNow();
  }

  private void ToggleFan(ControlState requested)
  {
    var requestedFanOn = requested == ControlState.FanOn;
    if (requestedFanOn == _fanOn)
      return;

    if (requestedFanOn)
    {
      logger.LogInformation("Activating Fan Relay (Fan - G).");
      relays.EnableFan();
    }
    else
    {
      logger.LogInformation("Deactivating Fan Relay (Fan - G).");
      relays.DisableFan();
    }

    _fanOn = requestedFanOn;
    RecordChange(ControlChangeKind.Fan, requested, requestedFanOn ? ControlState.FanOff : ControlState.FanOn, _lastFanChangeAt, null);
    _lastFanChangeAt = time.GetUtcNow();
  }

  private void ScheduleDwellTransition(TimeSpan delay)
  {
    _pendingDwellCts = new CancellationTokenSource();
    _pendingDwellTask = Task.Delay(delay, time, _pendingDwellCts.Token);
  }

  private void CancelPendingDwellTransition()
  {
    if (_pendingDwellCts is null)
      return;

    _pendingDwellCts.Cancel();
    _pendingDwellCts.Dispose();
    _pendingDwellCts = null;
    _pendingDwellTask = null;
  }

  private void TransitionToCall(HvacCall call, string? reason)
  {
    var previousCall = _currentCall;
    var previousSince = _lastCallChangeAt == DateTimeOffset.MinValue ? (DateTimeOffset?)null : _lastCallChangeAt;

    _currentCall = call;
    _lastCallChangeAt = time.GetUtcNow();

    switch (call)
    {
      case HvacCall.Dwell:
        if (previousCall == HvacCall.Heating)
          _lastHeatingEndedAt = time.GetUtcNow();

        if (previousCall == HvacCall.Cooling)
          _lastCoolingEndedAt = time.GetUtcNow();

        logger.LogInformation("Deactivating HVAC relays.");
        relays.EnableDwell();
        break;
      case HvacCall.Heating:
        logger.LogInformation("Activating Heating Relay (Heat - W).");
        relays.EnableHeating();
        break;
      case HvacCall.Cooling:
        logger.LogInformation("Activating Cooling Relay (Cool - Y).");
        relays.EnableCooling();
        break;
    }

    RecordChange(ControlChangeKind.Call, ToControlState(call), ToControlState(previousCall), previousSince, reason);
  }

  /// <summary>
  /// Queues a record of a relay actuation. Called after the relay has moved, so hardware that failed to actuate
  /// leaves no trace of a change that never happened.
  /// </summary>
  private void RecordChange(ControlChangeKind kind, ControlState state, ControlState? previousState, DateTimeOffset? previousSince, string? reason)
  {
    // CreatedAt is deliberately left alone; the persistence layer stamps it from the same clock. The duration is
    // measured here instead of being derived from the stored timeline so it stays accurate if a save is delayed.
    var change = new ControlStateChange
    {
      Kind = kind,
      State = state,
      PreviousState = previousState,
      PreviousStateDurationSeconds = previousSince is null ? null : (time.GetUtcNow() - previousSince.Value).TotalSeconds,
      Reason = reason ?? _currentContext?.Reason,
      EnvironmentTemperatureC = _currentContext?.EnvironmentTemperatureC,
      HumidityPercentage = _currentContext?.HumidityPercentage,
      TargetTemperatureC = _currentContext?.TargetTemperatureC,
      CO2LevelPpm = _currentContext?.CO2LevelPpm,
      HysteresisC = _currentContext?.HysteresisC,
      ForecastTemperatureC = _currentContext?.ForecastTemperatureC,
      Mode = _currentContext?.Mode,
      ScheduleId = _currentContext?.ScheduleId,
      SetPointId = _currentContext?.SetPointId,
    };
    _pendingChanges.Add(change);
  }

  /// <summary>
  /// Broadcasts and persists everything the last actuation queued up. Failures are logged and swallowed: both are
  /// reporting concerns and must never stop the equipment from being controlled.
  /// </summary>
  private async Task FlushChangesAsync(CancellationToken cancellationToken)
  {
    if (_pendingChanges.Count == 0)
      return;

    try
    {
      foreach (var change in _pendingChanges)
      {
        try
        {
          await hub.Clients.All.ControlStateChanged(ControlStateChangeDto.FromEntity(change));
        }
        catch (Exception ex)
        {
          // The change is still persisted, so a client that missed the broadcast can read the current state.
          logger.LogError(ex, "Failed to broadcast the {Kind} state change to {State}.", change.Kind, change.State);
        }

        var result = await dispatcher.DispatchAsync(new SaveControlStateChangeRequest(change), cancellationToken);
        if (result.IsFailure)
          logger.LogError("Failed to record the {Kind} state change to {State}: {Error}", change.Kind, change.State, result.Error.Message);
      }
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "An error occurred while recording control state changes.");
    }
    finally
    {
      _pendingChanges.Clear();
    }
  }

  private static ControlState ToControlState(HvacCall call) =>
    call switch
    {
      HvacCall.Heating => ControlState.Heating,
      HvacCall.Cooling => ControlState.Cooling,
      _ => ControlState.Dwell,
    };

  private static bool TryGetCall(ControlState state, out HvacCall call)
  {
    switch (state)
    {
      case ControlState.Dwell:
        call = HvacCall.Dwell;
        return true;
      case ControlState.Heating:
        call = HvacCall.Heating;
        return true;
      case ControlState.Cooling:
        call = HvacCall.Cooling;
        return true;
      default:
        call = HvacCall.Dwell;
        return false;
    }
  }

  /// <summary>
  /// Seeds the minimum off-time clock from the last persisted call change, so a restart moments after a call ended
  /// cannot let the guard re-energize the equipment immediately.
  /// </summary>
  private async Task RestoreCallClockAsync(CancellationToken cancellationToken)
  {
    try
    {
      var latest = await GetLatestChangeAsync(ControlChangeKind.Call, cancellationToken);
      if (latest is null)
        return;

      if (latest.State == ControlState.Dwell)
      {
        _lastCallChangeAt = latest.ChangedAt;
        return;
      }

      // The service went down mid-call and Initialize released the relays, so the call effectively ended just now.
      _lastCallChangeAt = time.GetUtcNow();
      logger.LogWarning("The service restarted while a {Call} call was active. Measuring the minimum off-time from startup.", latest.State);

      // The restart released the relays with it, so the call timeline must not show the call still running.
      RecordChange(ControlChangeKind.Call, ControlState.Dwell, latest.State, latest.ChangedAt, "the restart released the call");
      await FlushChangesAsync(cancellationToken);
    }
    catch (Exception ex)
    {
      // Without history the safe assumption is that a call ended moments ago; enforce a full minimum off-time.
      _lastCallChangeAt = time.GetUtcNow();
      logger.LogError(ex, "Failed to restore the call clock from history. Measuring the minimum off-time from startup.");
    }
  }

  private async Task RecordStartupEventAsync(CancellationToken cancellationToken)
  {
    try
    {
      var previous = await GetLatestLifecycleStateAsync(cancellationToken);
      ControlState? previousState = previous?.State == ControlState.Fault ? ControlState.Fault : null;
      var previousSince = previousState is null ? null : previous?.ChangedAt;

      RecordChange(ControlChangeKind.Lifecycle, ControlState.Startup, previousState, previousSince, "control service started");
      await FlushChangesAsync(cancellationToken);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "An error occurred while recording the control service startup event.");
    }
  }

  private async Task RecordFaultEventAsync(string reason, CancellationToken cancellationToken)
  {
    try
    {
      var previous = await GetLatestLifecycleStateAsync(cancellationToken);
      ControlState? previousState = previous?.State is ControlState.Startup or ControlState.Fault ? previous.State : null;
      var previousSince = previousState is null ? null : previous?.ChangedAt;

      RecordChange(ControlChangeKind.Lifecycle, ControlState.Fault, previousState, previousSince, reason);
      await FlushChangesAsync(cancellationToken);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "An error occurred while recording the control service fault event.");
    }
  }

  private Task<GetLatestControlStateChangeResponse?> GetLatestLifecycleStateAsync(CancellationToken cancellationToken) =>
    GetLatestChangeAsync(ControlChangeKind.Lifecycle, cancellationToken);

  private async Task<GetLatestControlStateChangeResponse?> GetLatestChangeAsync(ControlChangeKind kind, CancellationToken cancellationToken)
  {
    var result = await dispatcher.DispatchAsync<GetLatestControlStateChangeRequest, GetLatestControlStateChangeResponse?>(
      new(kind),
      cancellationToken
    );

    result.EnsureSuccess();
    return result.Value;
  }
}
