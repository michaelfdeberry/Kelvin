using System.Collections.Concurrent;

namespace Kelvin.Server.Models;

public class EnvironmentReading
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

  public EnvironmentReading Clone() =>
    new()
    {
      Timestamp = Timestamp,
      TemperatureC = TemperatureC,
      HumidityPercentage = HumidityPercentage,
      CO2LevelPpm = CO2LevelPpm,
      Areas = new(
        Areas.Select(x => new KeyValuePair<Guid, SensorPacket>(
          x.Key,
          new SensorPacket
          {
            SensorId = x.Value.SensorId,
            TemperatureC = x.Value.TemperatureC,
            HumidityPercentage = x.Value.HumidityPercentage,
            CO2LevelPpm = x.Value.CO2LevelPpm,
            BatteryLevelPercentage = x.Value.BatteryLevelPercentage,
            CreatedAt = x.Value.CreatedAt,
          }
        ))
      ),
    };
}
