using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Gateways;
using Kelvin.Server.Models;

namespace Kelvin.Server.Services;

public class ControlService(
  ILogger<ControlService> logger,
  IControlChannel controlChannel,
  IDispatcher dispatcher,
  IRelayController relays,
  IHostApplicationLifetime lifetime
) : BackgroundService
{
  private static readonly Guid subscriberId = Guid.NewGuid();

  // Hardware safety configurations
  private const int DefaultMinimumOffDurationMinutes = 5;
  private const int DefaultMinimumOnDurationMinutes = 3;

  // State tracking
  private ControlState _currentState = ControlState.Dwell;
  private DateTimeOffset _lastStateChangeTime = DateTimeOffset.MinValue;
  private CancellationTokenSource? _pendingDwellCts;
  private Task? _pendingDwellTask;

  public override Task StartAsync(CancellationToken cancellationToken)
  {
    relays.Initialize();
    return base.StartAsync(cancellationToken);
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        using var readCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var controlMessageTask = controlChannel.ReadAsync(subscriberId, readCancellation.Token);

        var gatewayResult = await dispatcher.DispatchAsync<GetGatewayRequest, GetGatewayResponse>(new(), stoppingToken);
        gatewayResult.EnsureSuccess();

        var gateway = gatewayResult.Value!;

        if (_pendingDwellTask is not null)
        {
          var completed = await Task.WhenAny(controlMessageTask, _pendingDwellTask);
          if (completed == _pendingDwellTask)
          {
            readCancellation.Cancel();
            CancelPendingDwellTransition();
            TransitionToState(ControlState.Dwell);
            continue;
          }
        }

        var controlMessage = await controlMessageTask;
        if (controlMessage is null)
          continue;

        relays.Configure(gateway);

        EvaluateAndActuate(gateway, controlMessage.State);
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

  private void EvaluateAndActuate(GetGatewayResponse gateway, ControlState requestedState)
  {
    // make sure to use the configured minimum durations, or fall back to defaults if not set
    var minOffDuration = TimeSpan.FromMinutes(gateway.MinimumOffDurationMinutes ?? DefaultMinimumOffDurationMinutes);
    var minOnDuration = TimeSpan.FromMinutes(gateway.MinimumOnDurationMinutes ?? DefaultMinimumOnDurationMinutes);
    var now = DateTimeOffset.UtcNow;
    var timeInCurrentState = now - _lastStateChangeTime;

    // Control belongs to the legacy thermostat while disabled, so every other request is ignored until
    // an Enable message arrives. No minimum duration is tracked for this, Disable blocks everything.
    if (_currentState == ControlState.Disable)
    {
      logger.LogInformation("Ignoring {RequestedState} request while Disabled.", requestedState);
      return;
    }

    // disabling control is either on demand by the user or due to a failure, so control should be reverted to the legacy thermostat immediately
    if (requestedState == ControlState.Disable)
    {
      if (_pendingDwellTask is not null)
      {
        logger.LogInformation("Received Disable while waiting to transition to Dwell; cancelling the wait.");
        CancelPendingDwellTransition();
      }

      if ((_currentState == ControlState.Heating || _currentState == ControlState.Cooling) && timeInCurrentState < minOnDuration)
      {
        logger.LogWarning("Disabling immediately even though Minimum On-Time ({RequiredMinutes}m) has not elapsed.", minOnDuration.TotalMinutes);
      }

      TransitionToState(ControlState.Disable);
      return;
    }

    // The fan can be toggled independently of other states and doesn't need to be tracked
    if (requestedState is ControlState.FanOn or ControlState.FanOff)
    {
      ToggleFan(requestedState);
      return;
    }

    // If the requested state is Enable, it means Kelvin is taking control from the legacy thermostat.
    if (requestedState == ControlState.Enable)
    {
      // this doesn't need to transition to state because if it wasn't enabled it's already in a state equivalent to Dwell
      // just set the current state to dwell and enable the relay.
      _currentState = ControlState.Dwell;
      relays.EnableControl();
      return;
    }

    if (requestedState == ControlState.Dwell)
    {
      if (_currentState != ControlState.Dwell)
      {
        // already waiting to transition to Dwell
        if (_pendingDwellTask is not null)
        {
          return;
        }

        // leaving an active Heating/Cooling call for Dwell is subject to the Minimum On-Time guard.
        if (timeInCurrentState < minOnDuration)
        {
          var remaining = minOnDuration - timeInCurrentState;
          logger.LogInformation(
            "Requested Dwell, but Minimum On-Time ({RequiredMinutes}m) has not elapsed. Transitioning to Dwell in {RemainingSeconds}s unless another call for {CurrentState} arrives first.",
            minOnDuration.TotalMinutes,
            remaining.TotalSeconds,
            _currentState
          );
          ScheduleDwellTransition(remaining);
          return;
        }

        TransitionToState(ControlState.Dwell);
      }
      return;
    }

    if (_currentState == ControlState.Dwell)
    {
      // Check Minimum Off-Time
      if (timeInCurrentState < minOffDuration)
      {
        logger.LogInformation(
          "Requested {RequestedState}, but Minimum Off-Time ({RequiredMinutes}m) has not elapsed. Blocked for {RemainingSeconds}s.",
          requestedState,
          minOffDuration.TotalMinutes,
          (minOffDuration - timeInCurrentState).TotalSeconds
        );

        // block activation
        return;
      }

      TransitionToState(requestedState);
    }
    else if (_currentState == requestedState)
    {
      if (_pendingDwellTask is not null)
      {
        logger.LogInformation(
          "Received another call for {RequestedState} while waiting to transition to Dwell; cancelling the wait.",
          requestedState
        );
        CancelPendingDwellTransition();
      }
    }
    else
    {
      // Heating and Cooling are the only two active states reachable here, so this guards against a direct Heating<->Cooling switch
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
        "Attempted to switch directly from {_currentState} to {requestedState} without an intermediate idle state. Ignoring the request.",
        _currentState,
        requestedState
      );
      TransitionToState(ControlState.Disable);
    }
  }

  private void ToggleFan(ControlState requestedState)
  {
    if (requestedState == ControlState.FanOn)
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
    _pendingDwellTask = Task.Delay(delay, _pendingDwellCts.Token);
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

  private void TransitionToState(ControlState newState)
  {
    _currentState = newState;
    _lastStateChangeTime = DateTimeOffset.UtcNow;

    switch (newState)
    {
      case ControlState.Enable:
        logger.LogInformation("Taking control from the Legacy Thermostat (Control - relay energized).");
        relays.EnableControl();
        break;
      case ControlState.Disable:
        logger.LogInformation("Deactivating HVAC relays, reverting control to Failsafe NC (Legacy Thermostat).");
        relays.DisableControl();
        break;
      case ControlState.Dwell:
        logger.LogInformation("Deactivating HVAC relays.");
        relays.EnableDwell();
        break;
      case ControlState.Heating:
        logger.LogInformation("Activating Heating Relay (Heat - W).");
        relays.EnableHeating();
        break;
      case ControlState.Cooling:
        logger.LogInformation("Activating Cooling Relay (Cool - Y).");
        relays.EnableCooling();
        break;
      default:
        logger.LogWarning("Unhandled ControlState: {newState}", newState);
        break;
    }
  }
}
