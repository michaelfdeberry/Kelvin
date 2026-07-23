namespace Kelvin.Server.Models;

public class Run : Entity
{
  /// <summary>
  /// The type of run, either heating or cooling. This determines whether the run is intended to raise or lower the temperature.
  /// </summary>
  public RunType Type { get; set; }

  /// <summary>
  /// The target temperature for the run in degrees Celsius.
  /// </summary>
  public int TargetTemperatureC { get; set; }

  /// <summary>
  /// The location temperature at which the run will be activated.
  /// </summary>
  public int? ActivationTemperatureC { get; set; }
}
