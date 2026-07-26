using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Gateways;
using Kelvin.Server.Models;

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
  IHostApplicationLifetime lifetime
) : BackgroundService
{
  private static readonly Guid subscriberId = Guid.NewGuid();

  // Hardware safety configurations
  private const int DEFAULT_MIN_OFF_DURATION_MINUTES = 5;
  private const int DEFAULT_MIN_ON_DURATION_MINUTES = 3;

  // State tracking
  private bool _controlEnabled;
  private HvacCall _currentCall = HvacCall.Dwell;
  private DateTimeOffset _lastCallChangeAt = DateTimeOffset.MinValue;
  private CancellationTokenSource? _pendingDwellCts;
  private Task? _pendingDwellTask;

  public override Task StartAsync(CancellationToken cancellationToken)
  {
    relays.Initialize();
    return base.StartAsync(cancellationToken);
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

        var gatewayResult = await dispatcher.DispatchAsync<GetGatewayRequest, GetGatewayResponse>(new(), stoppingToken);
        gatewayResult.EnsureSuccess();

        var gateway = gatewayResult.Value!;

        // A scheduled dwell never delays anything else: whichever of the two completes first is dealt with in this
        // iteration, and a message still in flight is picked up by the next one.
        if (_pendingDwellTask is not null && await Task.WhenAny(outstandingRead, _pendingDwellTask) == _pendingDwellTask)
        {
          CancelPendingDwellTransition();
          TransitionToCall(HvacCall.Dwell);
          continue;
        }

        var read = outstandingRead;
        outstandingRead = null;

        var controlMessage = await read;
        if (controlMessage is null)
          continue;

        relays.Configure(gateway);

        Handle(gateway, controlMessage.State);
      }
      catch (OperationCanceledException)
      {
        logger.LogInformation("ControlService is stopping due to cancellation.");
      }
      catch (GpioUnavailableException ex)
      {
        // never continue the state machine on unusable hardware; fail loudly instead of pretending to actuate
        logger.LogCritical(ex, "GPIO became unusable. Stopping the application so the failure is not silently ignored.");
        lifetime.StopApplication();
        return;
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "An error occurred in ControlService while processing control commands.");
      }
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
    // make sure to use the configured minimum durations, or fall back to defaults if not set
    var minOffDuration = TimeSpan.FromMinutes(gateway.MinimumOffDurationMinutes ?? DEFAULT_MIN_OFF_DURATION_MINUTES);
    var minOnDuration = TimeSpan.FromMinutes(gateway.MinimumOnDurationMinutes ?? DEFAULT_MIN_ON_DURATION_MINUTES);
    var timeInCurrentCall = time.GetUtcNow() - _lastCallChangeAt;

    if (call == HvacCall.Dwell)
    {
      if (_currentCall == HvacCall.Dwell)
        return;

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

      TransitionToCall(HvacCall.Dwell);
      return;
    }

    if (_currentCall == HvacCall.Dwell)
    {
      // Check Minimum Off-Time
      if (timeInCurrentCall < minOffDuration)
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

      TransitionToCall(call);
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
      // There will need to be some validation against this being configured in this way.
      //
      // This is being treated as a critical error because it shouldn't be possible to reach this point without a bug in the code.
      // Reverting control because going from heating to cooling may crack the heater core and going from cooling to heating may
      // damage the AC compressor, which is a safety concern.
      //
      // My current assumptions is that normal thermostats are dumb and the furnace control board is designed to handle this,
      // I wouldn't bet my furnace on it though.
      // TODO: research this topic more.
      logger.LogCritical(
        "Attempted to switch directly from {CurrentCall} to {RequestedCall} without an intermediate idle state. Ignoring the request.",
        _currentCall,
        call
      );
      DisableControl(gateway, "an unsafe call transition was requested");
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

    logger.LogInformation("Deactivating HVAC relays, reverting control to Failsafe NC (Legacy Thermostat) because {Reason}.", reason);
    _controlEnabled = false;
    _currentCall = HvacCall.Dwell;
    _lastCallChangeAt = time.GetUtcNow();
    relays.DisableControl();
  }

  private void ToggleFan(ControlState requested)
  {
    if (requested == ControlState.FanOn)
    {
      logger.LogInformation("Activating Fan Relay (Fan - G).");
      relays.EnableFan();
    }
    else
    {
      logger.LogInformation("Deactivating Fan Relay (Fan - G).");
      relays.DisableFan();
    }
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

  private void TransitionToCall(HvacCall call)
  {
    _currentCall = call;
    _lastCallChangeAt = time.GetUtcNow();

    switch (call)
    {
      case HvacCall.Dwell:
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
  }

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
}
