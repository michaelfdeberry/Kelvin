namespace Kelvin.Server.Models;

public class WeatherForecast
{
  public double Latitude { get; set; }

  public double Longitude { get; set; }

  public string TimeZone { get; set; } = string.Empty;

  public DateTimeOffset RetrievedAt { get; set; }

  public WeatherCurrent? Current { get; set; }

  public IReadOnlyList<WeatherForecastDay> Daily { get; set; } = [];
}

public class WeatherCurrent
{
  public DateTimeOffset Timestamp { get; set; }

  public double TemperatureC { get; set; }

  public double ApparentTemperatureC { get; set; }

  public double Humidity { get; set; }

  public double WindSpeedKph { get; set; }

  public int WeatherCode { get; set; }

  public string Summary { get; set; } = string.Empty;
}

public class WeatherForecastDay
{
  public DateOnly Date { get; set; }

  public double TemperatureMinC { get; set; }

  public double TemperatureMaxC { get; set; }

  public int WeatherCode { get; set; }

  public string Summary { get; set; } = string.Empty;
}
