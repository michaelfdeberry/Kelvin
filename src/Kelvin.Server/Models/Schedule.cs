using System.ComponentModel.DataAnnotations.Schema;

namespace Kelvin.Server.Models;

public class Schedule : Entity
{
  /// <summary>
  /// The type of schedule, either heating or cooling. This determines whether the schedule is intended to raise or lower the temperature.
  /// </summary>
  public RunType Type { get; set; }

  /// <summary>
  /// The time at which the schedule will start. The schedule will be active from StartTime to EndTime.
  /// </summary>
  public TimeOnly StartTime { get; set; }

  /// <summary>
  /// The time at which the schedule will end. The schedule will be active from StartTime to EndTime.
  /// </summary>
  public TimeOnly EndTime { get; set; }

  /// <summary>
  /// The target temperature for the schedule in degrees Celsius.
  /// </summary>
  public float TargetTemperatureC { get; set; }

  public Guid ThermostatId { get; set; }

  [ForeignKey("ThermostatId")]
  public virtual Thermostat? Thermostat { get; set; }
}
