namespace Kelvin.Server.Models;

// Phase one, control will be pretty basic, automatic mode will be the only thing that can't be done with a basic thermostat.
// Phase two, I plan to add more programmable features with things like after heating, after cooling, etc. along with support for controlling humidifiers and dehumidifiers.
// Phase three will be multiple thermostats/zones, If the mini split in my garage can support it.
// Phase four, if I ever get will be support for automated venting based on C02 levels, and possibly other air quality metrics.
public enum RunMode
{
  /// <summary>
  /// When disabled control is reverted to the dumb thermostat and the system will be idle.
  /// To be determined if the data collection will continue when the system is disabled,
  /// or if it will be paused until the system is enabled again.
  /// </summary>
  Disabled,

  /// <summary>
  /// The system is enabled, but the thermostat is not actively heating or cooling.
  /// The system will remain idle until the user enables another mode.
  /// </summary>
  Off,

  /// <summary>
  /// If the forecast temperature is at or below the target location temperature, the system will heat based on the set point or schedule target temperature.
  /// If the environment temperature is below the set point or schedule target temperature, the system will heat.
  /// If the environment temperature is above the set point or schedule target temperature, the system will remain idle.
  /// </summary>
  Heating,

  /// <summary>
  /// If the forecast temperature is at or above the target location temperature, the system will cool based on the set point or schedule target temperature.
  /// If the environment temperature is above the set point or schedule target temperature, the system will cool.
  /// If the environment temperature is below the set point or schedule target temperature, the system will remain idle.
  /// </summary>
  Cooling,

  /// <summary>
  /// The system automatically switches between heating and cooling.
  /// If a current location and target location temperature are provided, the system will use that to determine whether to
  /// heat or cool based on the environment temperature.
  ///
  /// When there is a location:
  /// If the forecast temperature is at or below the target location temperature
  /// the system will heat based on the environment temperature and the set point or schedule target temperature.
  ///
  /// If the forecast temperature is at or above the target location temperature
  /// the system will cool based on the environment temperature and the set point or schedule target temperature.
  ///
  /// When there isn't a location:
  /// If the environment temperature is below the set point or schedule target temperature, the system will heat.
  /// If the environment temperature is above the set point or schedule target temperature, the system will cool.
  ///
  /// otherwise, the system will remain idle.
  /// </summary>
  Automatic,
}
