namespace Kelvin.Server.Models;

public class WeatherCurrent
{
  public DateTimeOffset Timestamp { get; set; }

  public float TemperatureC { get; set; }

  public float ApparentTemperatureC { get; set; }

  public float Humidity { get; set; }

  public float WindSpeedKph { get; set; }

  public int WeatherCode { get; set; }

  public string Summary { get; set; } = string.Empty;
}
