using System.ComponentModel.DataAnnotations;

namespace Kelvin.Server.Models;

/// <summary>
/// The indoor humidity to aim for while the outdoor temperature is at or below <see cref="OutdoorTemperatureC" />
/// (and above the next colder target's).
/// </summary>
public record HumidityTarget(float OutdoorTemperatureC, float HumidityPercentage);

public class HumidifierConfiguration : DeviceConfiguration
{
  public static List<HumidityTarget> DefaultTargets() =>
    [new(-29, 15), new(-23, 20), new(-18, 25), new(-12, 30), new(-7, 35), new(-1, 40), new(4, 45)];

  /// <summary>
  /// First month (1-12) of the humidification season, inclusive.
  /// </summary>
  [Range(1, 12)]
  public int StartMonth { get; set; } = 10;

  /// <summary>
  /// Last month (1-12) of the humidification season, inclusive. May be earlier than <see cref="StartMonth" /> to wrap the year.
  /// </summary>
  [Range(1, 12)]
  public int EndMonth { get; set; } = 3;

  /// <summary>
  /// The humidity targets for different outdoor temperatures.
  /// </summary>
  public List<HumidityTarget> Targets { get; set; } = DefaultTargets();

  /// <summary>
  /// Checks if a month is in the humidification season.
  /// </summary>
  public bool IsInSeason(int month) => StartMonth <= EndMonth ? month >= StartMonth && month <= EndMonth : month >= StartMonth || month <= EndMonth;

  /// <summary>
  /// The target for the coldest threshold the outdoor temperature doesn't exceed. Null (humidifier off) when it's
  /// warmer than every threshold or no targets are configured.
  /// </summary>
  public float? TargetHumidityFor(float outdoorTemperatureC) =>
    Targets
      .OrderBy(target => target.OutdoorTemperatureC)
      .FirstOrDefault(target => outdoorTemperatureC <= target.OutdoorTemperatureC)
      ?.HumidityPercentage;
}
