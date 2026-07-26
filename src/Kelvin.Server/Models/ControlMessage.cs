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
  /// The system is dwelling and not actively heating or cooling.
  /// </summary>
  Dwell,

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

/// <summary>
/// The HVAC call the system is currently making. This is internal state, not part of the control message contract:
/// it covers only the states the minimum on/off duration guards apply to, so control ownership
/// (<see cref="ControlState.Enable" />/<see cref="ControlState.Disable" />) and the fan cannot reach that logic.
/// </summary>
public enum HvacCall
{
  /// <summary>No active call; the system is idle.</summary>
  Dwell,

  /// <summary>Actively calling for heat.</summary>
  Heating,

  /// <summary>Actively calling for cooling.</summary>
  Cooling,
}

/// <summary>
/// The conditions that led a producer to request a control state. Purely informational: it never influences what
/// the control service actuates, it is only carried through so a recorded state change can explain itself.
/// </summary>
/// <remarks>
/// Everything is nullable because a producer only fills in what it actually knows. A control message raised by an
/// API call has little more than the run mode, while one raised by the thermostat loop carries the full picture.
/// </remarks>
public record ControlContext(
  float? EnvironmentTemperatureC = null,
  float? HumidityPercentage = null,
  float? TargetTemperatureC = null,
  float? HysteresisC = null,
  float? ForecastTemperatureC = null,
  float? CO2LevelPpm = null,
  RunMode? Mode = null,
  Guid? ScheduleId = null,
  Guid? SetPointId = null,
  string? Reason = null
);

public record ControlMessage(ControlState State, ControlContext? Context = null);
