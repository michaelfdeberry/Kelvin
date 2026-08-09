using System.ComponentModel.DataAnnotations.Schema;

namespace Kelvin.Server.Models;

public class SetPoint : Entity
{
  /// <summary>
  /// The type of run, either heating or cooling. This determines whether the run is intended to raise or lower the temperature.
  /// </summary>
  public RunType Type { get; set; }

  /// <summary>
  /// The target temperature for the run in degrees Celsius.
  /// </summary>
  public float TargetTemperatureC { get; set; }

  public Guid ThermostatId { get; set; }

  [ForeignKey("ThermostatId")]
  public virtual Thermostat? Thermostat { get; set; }
}
