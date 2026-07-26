using System.Collections.Concurrent;

namespace Kelvin.Server.Models;

public class Environment
{
  /// <summary>
  /// The timestamp of the environment reading
  /// </summary>
  public DateTimeOffset Timestamp { get; set; }

  /// <summary>
  /// The average temperature in the environment, in degrees Celsius
  /// </summary>
  public float TemperatureC { get; set; }

  /// <summary>
  /// The average humidity in the environment, as a percentage
  /// </summary>
  public float HumidityPercentage { get; set; }

  /// <summary>
  /// The average CO2 level in the environment, in parts per million (ppm)
  /// </summary>
  public float CO2LevelPpm { get; set; }

  /// <summary>
  /// A dictionary of the latest sensor packets for each area in the environment, keyed by area ID
  /// </summary>
  public ConcurrentDictionary<Guid, SensorPacket> Areas { get; set; } = new ConcurrentDictionary<Guid, SensorPacket>();
}
