namespace Kelvin.Server.Models;

public enum TemperatureUnit
{
  Celsius,
  Fahrenheit,
}

public enum TimeFormat
{
  Hour12,
  Hour24,
}

public class Preferences : Entity
{
  public TemperatureUnit TemperatureUnit { get; set; }

  public TimeFormat TimeFormat { get; set; }

  public long? LocationId { get; set; }

  public string? LocationName { get; set; }
}
