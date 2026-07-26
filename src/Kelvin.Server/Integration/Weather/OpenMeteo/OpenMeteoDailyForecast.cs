using System.Text.Json.Serialization;

namespace Kelvin.Server.Integration.Weather;

public sealed partial class OpenMeteoWeatherApi
{
  private sealed class OpenMeteoDailyForecast
  {
    [JsonPropertyName("time")]
    public List<string> Time { get; set; } = [];

    [JsonPropertyName("temperature_2m_max")]
    public List<float> Temperature2mMax { get; set; } = [];

    [JsonPropertyName("temperature_2m_min")]
    public List<float> Temperature2mMin { get; set; } = [];

    [JsonPropertyName("weather_code")]
    public List<int> WeatherCode { get; set; } = [];
  }
}
