namespace Kelvin.Server.Models;

public enum TemperatureUnit
{
  Celsius = 0,
  Fahrenheit = 1,
}

public enum TimeFormat
{
  Hour24 = 0,
  Hour12 = 1,
}

public class Preferences : Entity
{
  public TemperatureUnit TemperatureUnit { get; set; }

  public TimeFormat TimeFormat { get; set; }

  public long? LocationId { get; set; }

  public string? LocationName { get; set; }
}
