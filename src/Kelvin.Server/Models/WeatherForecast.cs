namespace Kelvin.Server.Models;

public class WeatherForecast
{
  public double Latitude { get; set; }

  public double Longitude { get; set; }

  public string TimeZone { get; set; } = string.Empty;

  public DateTimeOffset RetrievedAt { get; set; }

  public WeatherCurrent? Current { get; set; }

  public IEnumerable<WeatherForecastDay> Daily { get; set; } = [];
}
