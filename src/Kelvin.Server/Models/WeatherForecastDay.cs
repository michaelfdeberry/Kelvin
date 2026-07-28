namespace Kelvin.Server.Models;

public class WeatherForecastDay
{
  public DateOnly Date { get; set; }

  public float TemperatureMinC { get; set; }

  public float TemperatureMaxC { get; set; }

  public int WeatherCode { get; set; }

  public string Summary { get; set; } = string.Empty;
}
