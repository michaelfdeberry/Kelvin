using System.Text.Json.Serialization;

namespace Kelvin.Server.Integration.Weather;

public sealed partial class OpenMeteoWeatherApi
{
  private sealed class OpenMeteoCurrentWeather
  {
    [JsonPropertyName("time")]
    public string Time { get; set; } = string.Empty;

    [JsonPropertyName("temperature_2m")]
    public float Temperature2m { get; set; }

    [JsonPropertyName("relative_humidity_2m")]
    public float RelativeHumidity2m { get; set; }

    [JsonPropertyName("apparent_temperature")]
    public float ApparentTemperature2m { get; set; }

    [JsonPropertyName("weather_code")]
    public int WeatherCode { get; set; }

    [JsonPropertyName("wind_speed_10m")]
    public float WindSpeed10m { get; set; }
  }
}
