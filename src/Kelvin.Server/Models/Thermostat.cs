namespace Kelvin.Server.Models;

public enum CO2RecirculationMode
{
  /// <summary>
  /// Will enabled the fan when the average CO2 is above the threshold.
  /// </summary>
  Average,

  /// <summary>
  /// Will enable the fan when any CO2 reading is above the threshold.
  /// </summary>
  Peak,
}

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
  /// Whether CO2 recirculation is enabled.
  /// </summary>
  public bool CO2RecirculationEnabled { get; set; } = false;

  /// <summary>
  /// The mode for CO2 recirculation.
  /// </summary>
  public CO2RecirculationMode CO2RecirculationMode { get; set; } = CO2RecirculationMode.Average;

  /// <summary>
  /// The CO2 level to trigger recirculation.
  /// </summary>
  public ushort CO2RecirculationTriggerPpm { get; set; } = 1000;

  /// <summary>
  /// The CO2 level to stop recirculation.
  /// </summary>
  public ushort CO2RecirculationSatisfiedPpm { get; set; } = 800;

  /// <summary>
  /// The cycle, fan on and off, duration for CO2 recirculation in milliseconds.
  /// </summary>
  public int CO2RecirculationDurationMs { get; set; } = 60000;

  /// <summary>
  /// When enabled Kelvin will enabled the fan in an attempt to balance heating.
  /// </summary>
  public bool HeatingAutoBalanceEnabled { get; set; } = false;

  /// <summary>
  /// The threshold for triggering heating balance.
  /// </summary>
  public float HeatingBalanceTriggerC { get; set; } = 3.0f;

  /// <summary>
  /// The threshold to consider heating balanced.
  /// </summary>
  public float HeatingBalanceSatisfiedC { get; set; } = 1.5f;

  /// <summary>
  /// Duration for heating auto balance in ms.
  /// </summary>
  public int HeatingAutoBalanceDurationMs { get; set; } = 60000;

  /// <summary>
  /// The set points for the thermostat.
  /// </summary>
  public virtual ICollection<SetPoint> SetPoints { get; set; } = [];

  /// <summary>
  /// The schedules for the thermostat.
  /// </summary>
  public virtual ICollection<Schedule> Schedules { get; set; } = [];
}
