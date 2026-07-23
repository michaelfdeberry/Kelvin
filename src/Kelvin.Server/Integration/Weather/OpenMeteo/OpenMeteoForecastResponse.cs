using System.Text.Json.Serialization;

namespace Kelvin.Server.Integration.Weather;

public sealed partial class OpenMeteoWeatherApi
{
  private sealed class OpenMeteoForecastResponse
  {
    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = string.Empty;

    [JsonPropertyName("current")]
    public OpenMeteoCurrentWeather? Current { get; set; }

    [JsonPropertyName("daily")]
    public OpenMeteoDailyForecast? Daily { get; set; }
  }
}
