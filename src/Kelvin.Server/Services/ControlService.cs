using System.Device.Gpio;
using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Gateways;
using Kelvin.Server.Models;

namespace Kelvin.Server.Services;

public class ControlService(
  ILogger<ControlService> logger,
  IControlChannel controlChannel,
  IDispatcher dispatcher,
  IConfiguration configuration,
  IHostApplicationLifetime lifetime
) : BackgroundService
{
  private static readonly Guid subscriberId = Guid.NewGuid();

  // Set Gpio:Required to false only for development on machines without GPIO hardware.
  // On the appliance it must stay true: a thermostat that logs "Activating Heating Relay" while
  // actuating nothing looks healthy and isn't.
  private const string GpioRequiredConfigurationKey = "Gpio:Required";

  // The relay board is active low: driving a pin LOW energizes the relay, HIGH releases it.
  private static readonly PinValue RelayOn = PinValue.Low;
  private static readonly PinValue RelayOff = PinValue.High;

  // Hardware safety configurations
  private const int DefaultMinimumOffDurationMinutes = 5;
  private const int DefaultMinimumOnDurationMinutes = 3;
  private TimeSpan minOffDuration;
  private TimeSpan minOnDuration;

  // State tracking
  private ControlState _currentState = ControlState.Idle;
  private DateTimeOffset _lastStateChangeTime = DateTimeOffset.MinValue;
  private Task? _transitionTimer;
  private ControlState? _delayedState;

  // GPIO
  private GpioController? _gpio;
  private bool _gpioRequired;
  private GetGatewayResponse? _gateway;

  public override Task StartAsync(CancellationToken cancellationToken)
  {
    _gpioRequired = configuration.GetValue(GpioRequiredConfigurationKey, true);

    try
    {
      _gpio = new GpioController();
    }
    catch (Exception ex)
    {
      if (_gpioRequired)
        throw new InvalidOperationException(
          "Unable to initialize the GPIO controller, so the HVAC relays cannot be actuated. On a Raspberry Pi 5 "
            + "System.Device.Gpio requires the libgpiod runtime (libgpiod.so.2 or libgpiod.so.3); install it and verify "
            + $"with 'gpiodetect'. Set {GpioRequiredConfigurationKey} to false to run without relay control.",
          ex
        );

      logger.LogWarning(ex, "GPIO is not available and {ConfigurationKey} is false. HVAC relays will not be actuated.", GpioRequiredConfigurationKey);
    }

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

        if (_transitionTimer is not null)
        {
          await Task.WhenAny(controlMessageTask, _transitionTimer);
          if (_transitionTimer.IsCompleted)
          {
            readCancellation.Cancel();
            _transitionTimer = null;
            var delayedState = _delayedState!.Value;
            _delayedState = null;
            await TransitionToState(delayedState);
            continue;
          }

          await controlMessageTask;
          logger.LogInformation("Ignoring control message while waiting to transition to {DelayedState}.", _delayedState);
          continue;
        }

        var controlMessage = await controlMessageTask;
        if (controlMessage is null)
          continue;

        var gatewayResult = await dispatcher.DispatchAsync<GetGatewayRequest, GetGatewayResponse>(new(), stoppingToken);
        gatewayResult.EnsureSuccess();

        var gateway = gatewayResult.Value!;
        // make sure to use the configured minimum durations, or fall back to defaults if not set
        minOffDuration = TimeSpan.FromMinutes(gateway.MinimumOffDurationMinutes ?? DefaultMinimumOffDurationMinutes);
        minOnDuration = TimeSpan.FromMinutes(gateway.MinimumOnDurationMinutes ?? DefaultMinimumOnDurationMinutes);

        ConfigureGpio(gateway);

        await EvaluateAndActuateAsync(controlMessage.State);
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

  private async Task EvaluateAndActuateAsync(ControlState requestedState)
  {
    var now = DateTimeOffset.UtcNow;
    var timeInCurrentState = now - _lastStateChangeTime;

    if (requestedState == ControlState.Enable)
    {
      if (_currentState == ControlState.Disable)
      {
        await TransitionToState(ControlState.Enable);
        return;
      }

      // already enabled; re-assert control without disturbing the current state or its minimum duration timers
      WritePin(_gateway?.ControlPin, RelayOn, nameof(GetGatewayResponse.ControlPin));
      return;
    }

    if (requestedState == ControlState.Disable)
    {
      if ((_currentState == ControlState.Heating || _currentState == ControlState.Cooling) && timeInCurrentState < minOnDuration)
      {
        var remaining = minOnDuration - timeInCurrentState;
        logger.LogInformation(
          "Requested Disabled, but Minimum On-Time ({RequiredMinutes}m) has not elapsed. Transitioning to Disabled in {RemainingSeconds}s and ignoring control messages until then.",
          minOnDuration.TotalMinutes,
          remaining.TotalSeconds
        );
        ScheduleTransition(ControlState.Disable, remaining);
        return;
      }

      await TransitionToState(ControlState.Disable);
      return;
    }

    if (requestedState == ControlState.Idle)
    {
      if (!IsInactive(_currentState))
      {
        // Check Minimum On-Time guard
        if (timeInCurrentState < minOnDuration)
        {
          logger.LogInformation(
            "Requested Idle/Off, but Minimum On-Time ({RequiredMinutes}m) has not elapsed. Holding current state for {RemainingSeconds}s.",
            minOnDuration.TotalMinutes,
            (minOnDuration - timeInCurrentState).TotalSeconds
          );
          return; // Hold state
        }

        await TransitionToState(ControlState.Idle);
      }
      return;
    }

    // Handle active heating or cooling requests
    if (IsInactive(_currentState))
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

      await TransitionToState(requestedState);
    }
    else if (_currentState != requestedState)
    {
      logger.LogWarning(
        "Attempted to switch state from {_currentState} to {requestedState} without an intermediate Idle state.",
        _currentState,
        requestedState
      );
    }
  }

  private void ScheduleTransition(ControlState state, TimeSpan delay)
  {
    _delayedState = state;
    _transitionTimer = Task.Delay(delay, CancellationToken.None);
  }

  // Enabled and Idle both mean Kelvin holds control with no call for heat or cool, so they gate transitions the same way.
  private static bool IsInactive(ControlState state) => state is ControlState.Idle or ControlState.Enable;

  private Task TransitionToState(ControlState newState)
  {
    _currentState = newState;
    _lastStateChangeTime = DateTimeOffset.UtcNow;

    switch (newState)
    {
      case ControlState.FanOn:
        logger.LogInformation("Activating Fan Relay (Fan - G).");
        WritePin(_gateway?.FanPin, RelayOn, nameof(GetGatewayResponse.FanPin));
        break;
      case ControlState.FanOff:
        logger.LogInformation("Deactivating Fan Relay (Fan - G).");
        WritePin(_gateway?.FanPin, RelayOff, nameof(GetGatewayResponse.FanPin));
        break;
      case ControlState.Heating:
        logger.LogInformation("Activating Heating Relay (Heat - W).");
        WritePin(_gateway?.CoolingPin, RelayOff, nameof(GetGatewayResponse.CoolingPin));
        WritePin(_gateway?.HeatingPin, RelayOn, nameof(GetGatewayResponse.HeatingPin));
        break;
      case ControlState.Cooling:
        logger.LogInformation("Activating Cooling Relay (Cool - Y).");
        WritePin(_gateway?.HeatingPin, RelayOff, nameof(GetGatewayResponse.HeatingPin));
        WritePin(_gateway?.CoolingPin, RelayOn, nameof(GetGatewayResponse.CoolingPin));
        break;
      case ControlState.Enable:
        logger.LogInformation("Taking control from the Legacy Thermostat (Control - relay energized).");
        WritePin(_gateway?.HeatingPin, RelayOff, nameof(GetGatewayResponse.HeatingPin));
        WritePin(_gateway?.CoolingPin, RelayOff, nameof(GetGatewayResponse.CoolingPin));
        WritePin(_gateway?.FanPin, RelayOff, nameof(GetGatewayResponse.FanPin));
        WritePin(_gateway?.ControlPin, RelayOn, nameof(GetGatewayResponse.ControlPin));
        break;
      case ControlState.Idle:
        logger.LogInformation("Deactivating HVAC relays.");
        WritePin(_gateway?.HeatingPin, RelayOff, nameof(GetGatewayResponse.HeatingPin));
        WritePin(_gateway?.CoolingPin, RelayOff, nameof(GetGatewayResponse.CoolingPin));
        break;
      case ControlState.Disable:
        logger.LogInformation("Deactivating HVAC relays, reverting control to Failsafe NC (Legacy Thermostat).");
        WritePin(_gateway?.HeatingPin, RelayOff, nameof(GetGatewayResponse.HeatingPin));
        WritePin(_gateway?.CoolingPin, RelayOff, nameof(GetGatewayResponse.CoolingPin));
        WritePin(_gateway?.FanPin, RelayOff, nameof(GetGatewayResponse.FanPin));
        WritePin(_gateway?.ControlPin, RelayOff, nameof(GetGatewayResponse.ControlPin));
        break;
      default:
        logger.LogWarning("Unhandled ControlState: {newState}", newState);
        break;
    }

    return Task.CompletedTask;
  }

  private void ConfigureGpio(GetGatewayResponse gateway)
  {
    ClosePinIfReplaced(_gateway?.HeatingPin, gateway.HeatingPin);
    ClosePinIfReplaced(_gateway?.CoolingPin, gateway.CoolingPin);
    ClosePinIfReplaced(_gateway?.FanPin, gateway.FanPin);
    ClosePinIfReplaced(_gateway?.ControlPin, gateway.ControlPin);

    _gateway = gateway;

    OpenPin(gateway.HeatingPin, nameof(gateway.HeatingPin));
    OpenPin(gateway.CoolingPin, nameof(gateway.CoolingPin));
    OpenPin(gateway.FanPin, nameof(gateway.FanPin));
    OpenPin(gateway.ControlPin, nameof(gateway.ControlPin));
  }

  private void ClosePinIfReplaced(int? previous, int? configured)
  {
    if (previous == configured || previous is not int pin || _gpio?.IsPinOpen(pin) != true)
      return;

    _gpio.Write(pin, RelayOff);
    _gpio.ClosePin(pin);
  }

  private void OpenPin(int? configured, string pinName)
  {
    // already open pins keep their current value; a pin that failed to open previously is retried here
    if (configured is not int pin || _gpio is null || _gpio.IsPinOpen(pin))
      return;

    try
    {
      // start released so the furnace stays on the failsafe path until a state is requested
      _gpio.OpenPin(pin, PinMode.Output, RelayOff);
      logger.LogInformation("Opened GPIO pin {Pin} for {PinName}.", pin, pinName);
    }
    catch (Exception ex)
    {
      if (_gpioRequired)
        throw new GpioUnavailableException($"Failed to open GPIO pin {pin} for {pinName}.", ex);

      logger.LogError(ex, "Failed to open GPIO pin {Pin} for {PinName}.", pin, pinName);
    }
  }

  private void WritePin(int? pin, PinValue value, string pinName)
  {
    if (pin is not int pinNumber)
      return;

    if (_gpio is null || !_gpio.IsPinOpen(pinNumber))
    {
      if (_gpioRequired)
        throw new GpioUnavailableException($"Unable to write {value} to {pinName}; GPIO pin {pinNumber} is not open.");

      logger.LogWarning("Unable to write {Value} to {PinName}; GPIO pin {Pin} is not open.", value, pinName, pinNumber);
      return;
    }

    try
    {
      _gpio.Write(pinNumber, value);
    }
    catch (Exception ex)
    {
      if (_gpioRequired)
        throw new GpioUnavailableException($"Failed to write {value} to GPIO pin {pinNumber} for {pinName}.", ex);

      logger.LogError(ex, "Failed to write {Value} to GPIO pin {Pin} for {PinName}.", value, pinNumber, pinName);
    }
  }

  public override void Dispose()
  {
    if (_gpio is not null)
    {
      foreach (var pin in new[] { _gateway?.HeatingPin, _gateway?.CoolingPin, _gateway?.FanPin, _gateway?.ControlPin })
      {
        if (pin is int pinNumber && _gpio.IsPinOpen(pinNumber))
        {
          // release every relay so the legacy thermostat regains control when the service stops
          _gpio.Write(pinNumber, RelayOff);
          _gpio.ClosePin(pinNumber);
        }
      }

      _gpio.Dispose();
      _gpio = null;
    }

    base.Dispose();
    GC.SuppressFinalize(this);
  }
}
