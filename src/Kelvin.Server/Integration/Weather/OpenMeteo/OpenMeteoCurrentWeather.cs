using System.Text.Json.Serialization;

namespace Kelvin.Server.Integration.Weather;

public sealed partial class OpenMeteoWeatherApi
{
  private sealed class OpenMeteoCurrentWeather
  {
    [JsonPropertyName("time")]
    public string Time { get; set; } = string.Empty;

    [JsonPropertyName("temperature_2m")]
    public double Temperature2m { get; set; }

    [JsonPropertyName("relative_humidity_2m")]
    public double RelativeHumidity2m { get; set; }

    [JsonPropertyName("apparent_temperature")]
    public double ApparentTemperature2m { get; set; }

    [JsonPropertyName("weather_code")]
    public int WeatherCode { get; set; }

    [JsonPropertyName("wind_speed_10m")]
    public double WindSpeed10m { get; set; }
  }
}
