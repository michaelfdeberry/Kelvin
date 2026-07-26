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

public class WeatherForecastDay
{
  public DateOnly Date { get; set; }

  public float TemperatureMinC { get; set; }

  public float TemperatureMaxC { get; set; }

  public int WeatherCode { get; set; }

  public string Summary { get; set; } = string.Empty;
}
