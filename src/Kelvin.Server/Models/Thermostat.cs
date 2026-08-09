namespace Kelvin.Server.Models;

public class Thermostat : Entity
{
  /// <summary>
  /// The current run mode of the thermostat. This can be one of the following values:
  /// </summary>
  public RunMode Mode { get; set; }

  /// <summary>
  ///   The current state of the fan. This can be one of the following values:
  /// </summary>
  public bool FanEnabled { get; set; }

  /// <summary>
  /// The temperature offset in degrees Celsius to apply to the hysteresis/dead-zone for the thermostat.
  /// </summary>
  public float HysteresisC { get; set; } = 0.6f;

  /// <summary>
  /// The forecasted temperature that will lock out heating if the temperature is above this value.
  /// </summary>
  public float? HeatingLockoutC { get; set; }

  /// <summary>
  /// The forecasted temperature that will lock out cooling if the temperature is below this value.
  /// </summary>
  public float? CoolingLockoutC { get; set; }

  /// <summary>
  /// The set points for the thermostat.
  /// </summary>
  public virtual ICollection<SetPoint> SetPoints { get; set; } = [];

  /// <summary>
  /// The schedules for the thermostat.
  /// </summary>
  public virtual ICollection<Schedule> Schedules { get; set; } = [];
}
