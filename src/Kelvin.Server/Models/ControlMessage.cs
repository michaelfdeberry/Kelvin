namespace Kelvin.Server.Models;

public enum ControlState
{
  /// <summary>
  /// The system is disabled and control is reverted to the dumb thermostat.
  /// </summary>
  Disable,

  /// <summary>
  /// The system is enabled and Kelvin takes control from the dumb thermostat.
  /// </summary>
  /// <remarks>
  /// This is the only state that energizes the control relay. Every other state leaves it untouched so control is
  /// only handed back by <see cref="Disable" />, which some error states fall back to.
  /// </remarks>
  Enable,

  /// <summary>
  /// The system is idle and not actively heating or cooling.
  /// </summary>
  Idle,

  /// <summary>
  /// The system is actively heating to reach the target temperature.
  /// </summary>
  Heating,

  /// <summary>
  /// The system is actively cooling to reach the target temperature.
  /// </summary>
  Cooling,

  /// <summary>
  /// The system is actively running the fan to circulate air without heating or cooling.
  /// </summary>
  FanOn,

  /// <summary>
  /// The system is not running the fan.
  /// </summary>
  FanOff,
}

public record ControlMessage(ControlState State);
