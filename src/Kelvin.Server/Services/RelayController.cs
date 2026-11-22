using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using Kelvin.Server.Application;
using Kelvin.Server.Features.Gateways;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;

namespace Kelvin.Server.Services;

public interface IRelayController
{
  /// <summary>Creates the GPIO controller. Throws when GPIO is required but unavailable.</summary>
  void Initialize();

  /// <summary>Opens the pins the gateway assigns to each relay, closing any pin that was reassigned.</summary>
  void Configure(GetGatewayResponse gateway);

  void EnableControl();
  void DisableControl();
  void EnableHeating();
  void DisableHeating();
  void EnableCooling();
  void DisableCooling();
  void EnableFan();
  void DisableFan();
  void EnableDwell();
  RelayState GetState();
}

public class RelayController(ILogger<RelayController> logger, IConfiguration configuration, IDispatcher dispatcher) : IRelayController, IDisposable
{
  // Set Gpio:Required to false only for development on machines without GPIO hardware.
  // On the appliance it must stay true: a thermostat that logs "Activating Heating Relay" while
  // actuating nothing looks healthy and isn't.
  private const string GpioRequiredConfigurationKey = "Gpio:Required";
  private const string GpioChipConfigurationKey = "Gpio:Chip";

  // The relay board is active low: driving a pin LOW energizes the relay, HIGH releases it.
  private static readonly PinValue RelayOn = PinValue.Low;
  private static readonly PinValue RelayOff = PinValue.High;

  private GpioController? _gpio;
  private bool _gpioRequired;
  private GetGatewayResponse? _gateway;

  private readonly RelayState _state = new();
  private volatile GetRelayStatesResponse _published = GetRelayStatesResponse.FromState(new RelayState());
  private int _flushing;

  public void Initialize()
  {
    _gpioRequired = configuration.GetValue(GpioRequiredConfigurationKey, true);
    var gpioChip = configuration.GetValue(GpioChipConfigurationKey, 0);

    try
    {
      _gpio = new GpioController(new LibGpiodDriver(gpioChip: gpioChip));
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
  }

  public void Configure(GetGatewayResponse gateway)
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

  public void EnableControl()
  {
    try
    {
      SetHeating(false);
      SetCooling(false);
      SetFan(false);
      SetControl(true);
    }
    finally
    {
      NotifyStateChanged();
    }
  }

  public void DisableControl()
  {
    try
    {
      SetHeating(false);
      SetCooling(false);
      SetFan(false);
      SetControl(false);
    }
    finally
    {
      NotifyStateChanged();
    }
  }

  public void EnableDwell()
  {
    try
    {
      SetHeating(false);
      SetCooling(false);
    }
    finally
    {
      NotifyStateChanged();
    }
  }

  public void EnableHeating()
  {
    SetHeating(true);
    NotifyStateChanged();
  }

  public void DisableHeating()
  {
    SetHeating(false);
    NotifyStateChanged();
  }

  public void EnableCooling()
  {
    SetCooling(true);
    NotifyStateChanged();
  }

  public void DisableCooling()
  {
    SetCooling(false);
    NotifyStateChanged();
  }

  public void EnableFan()
  {
    SetFan(true);
    NotifyStateChanged();
  }

  public void DisableFan()
  {
    SetFan(false);
    NotifyStateChanged();
  }

  public RelayState GetState() => _state;

  // The Set* methods move a single relay without publishing, so a public method that moves several relays publishes once, when it is done.
  // The composites publish in a finally so a failed write still reports the relays that did move.
  private void SetControl(bool on)
  {
    WritePin(_gateway?.ControlPin, on ? RelayOn : RelayOff, nameof(GetGatewayResponse.ControlPin));
    _state.Control = on;
  }

  private void SetHeating(bool on)
  {
    WritePin(_gateway?.HeatingPin, on ? RelayOn : RelayOff, nameof(GetGatewayResponse.HeatingPin));
    _state.Heating = on;
  }

  private void SetCooling(bool on)
  {
    WritePin(_gateway?.CoolingPin, on ? RelayOn : RelayOff, nameof(GetGatewayResponse.CoolingPin));
    _state.Cooling = on;
  }

  private void SetFan(bool on)
  {
    WritePin(_gateway?.FanPin, on ? RelayOn : RelayOff, nameof(GetGatewayResponse.FanPin));
    _state.Fan = on;
  }

  /// <summary>
  /// Publishes off the calling thread so actuating a relay never waits on subscribers. A single flush runs at a time and always
  /// publishes the latest state, so a burst of moves is coalesced and events can't arrive out of order.
  /// </summary>
  private void NotifyStateChanged()
  {
    if (!HasUnpublishedChange() || Interlocked.Exchange(ref _flushing, 1) == 1)
      return;

    _ = Task.Run(FlushAsync);
  }

  private async Task FlushAsync()
  {
    do
    {
      while (HasUnpublishedChange())
      {
        var current = GetRelayStatesResponse.FromState(_state);
        _published = current;

        try
        {
          await dispatcher.PublishAsync(new RelayStatesChangedEvent(current));
        }
        catch (Exception ex)
        {
          logger.LogError(ex, "Failed to publish the relay states.");
        }
      }

      Volatile.Write(ref _flushing, 0);
      // A move can land between the last check and clearing the flag, when its notification saw a flush still running.
    } while (HasUnpublishedChange() && Interlocked.Exchange(ref _flushing, 1) == 0);
  }

  private bool HasUnpublishedChange() => GetRelayStatesResponse.FromState(_state) != _published;

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

  public void Dispose()
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

    GC.SuppressFinalize(this);
  }
}
