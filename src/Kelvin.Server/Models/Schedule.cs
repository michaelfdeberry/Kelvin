namespace Kelvin.Server.Models;

public class Schedule : Entity
{
  /// <summary>
  /// The type of schedule, either heating or cooling. This determines whether the schedule is intended to raise or lower the temperature.
  /// </summary>
  public RunType Type { get; set; }

  /// <summary>
  /// Indicates whether the schedule is enabled or disabled. If disabled, the schedule will not be active regardless of the time or temperature conditions.
  /// </summary>
  public bool Enabled { get; set; }

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
  public int TargetTemperatureC { get; set; }

  /// <summary>
  /// The location temperature at which the schedule will be activated.
  /// </summary>
  public int? ActivationTemperatureC { get; set; }
}
