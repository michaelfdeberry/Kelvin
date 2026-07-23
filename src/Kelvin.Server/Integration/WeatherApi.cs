using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kelvin.Server.Models;

namespace Kelvin.Server.Integration;

public interface IWeatherApi
{
  Task<WeatherForecast?> GetForecastAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}

public sealed class MeteoWeatherApi(IHttpClientFactory httpClientFactory) : IWeatherApi
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

  private const string ClientName = "OpenMeteo";

  public async Task<WeatherForecast?> GetForecastAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
  {
    var httpClient = httpClientFactory.CreateClient(ClientName);
    var requestUri =
      $"forecast?latitude={latitude.ToString(CultureInfo.InvariantCulture)}&longitude={longitude.ToString(CultureInfo.InvariantCulture)}&timezone=auto&forecast_days=7&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m&daily=weather_code,temperature_2m_max,temperature_2m_min";

    var response = await httpClient.GetFromJsonAsync<MeteoForecastResponse>(requestUri, JsonOptions, cancellationToken);
    if (response is null)
      return null;

    return new WeatherForecast
    {
      Latitude = response.Latitude,
      Longitude = response.Longitude,
      TimeZone = response.Timezone,
      RetrievedAt = DateTimeOffset.UtcNow,
      Current = response.Current is null
        ? null
        : new WeatherCurrent
        {
          Timestamp = ParseDateTimeOffset(response.Current.Time),
          TemperatureC = response.Current.Temperature2m,
          ApparentTemperatureC = response.Current.ApparentTemperature2m,
          Humidity = response.Current.RelativeHumidity2m,
          WindSpeedKph = response.Current.WindSpeed10m,
          WeatherCode = response.Current.WeatherCode,
          Summary = DescribeWeatherCode(response.Current.WeatherCode),
        },
      Daily = response.Daily is null ? [] : BuildDailyForecast(response.Daily),
    };
  }

  private static IReadOnlyList<WeatherForecastDay> BuildDailyForecast(MeteoDailyForecast daily)
  {
    var count = new[] { daily.Time.Count, daily.Temperature2mMax.Count, daily.Temperature2mMin.Count, daily.WeatherCode.Count }.Min();

    var forecast = new List<WeatherForecastDay>(count);

    for (var index = 0; index < count; index++)
    {
      forecast.Add(
        new WeatherForecastDay
        {
          Date = DateOnly.Parse(daily.Time[index], CultureInfo.InvariantCulture),
          TemperatureMaxC = daily.Temperature2mMax[index],
          TemperatureMinC = daily.Temperature2mMin[index],
          WeatherCode = daily.WeatherCode[index],
          Summary = DescribeWeatherCode(daily.WeatherCode[index]),
        }
      );
    }

    return forecast;
  }

  private static DateTimeOffset ParseDateTimeOffset(string value)
  {
    if (
      DateTimeOffset.TryParse(
        value,
        CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
        out var timestamp
      )
    )
      return timestamp;

    return DateTimeOffset.UtcNow;
  }

  private static string DescribeWeatherCode(int weatherCode) =>
    weatherCode switch
    {
      0 => "Clear sky",
      1 => "Mainly clear",
      2 => "Partly cloudy",
      3 => "Overcast",
      45 => "Fog",
      48 => "Depositing rime fog",
      51 => "Light drizzle",
      53 => "Moderate drizzle",
      55 => "Dense drizzle",
      56 => "Light freezing drizzle",
      57 => "Dense freezing drizzle",
      61 => "Slight rain",
      63 => "Moderate rain",
      65 => "Heavy rain",
      66 => "Light freezing rain",
      67 => "Heavy freezing rain",
      71 => "Slight snow fall",
      73 => "Moderate snow fall",
      75 => "Heavy snow fall",
      77 => "Snow grains",
      80 => "Slight rain showers",
      81 => "Moderate rain showers",
      82 => "Violent rain showers",
      85 => "Slight snow showers",
      86 => "Heavy snow showers",
      95 => "Thunderstorm",
      96 => "Thunderstorm with slight hail",
      99 => "Thunderstorm with heavy hail",
      _ => "Unknown",
    };

  private sealed class MeteoForecastResponse
  {
    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = string.Empty;

    [JsonPropertyName("current")]
    public MeteoCurrentWeather? Current { get; set; }

    [JsonPropertyName("daily")]
    public MeteoDailyForecast? Daily { get; set; }
  }

  private sealed class MeteoCurrentWeather
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

  private sealed class MeteoDailyForecast
  {
    [JsonPropertyName("time")]
    public List<string> Time { get; set; } = [];

    [JsonPropertyName("temperature_2m_max")]
    public List<double> Temperature2mMax { get; set; } = [];

    [JsonPropertyName("temperature_2m_min")]
    public List<double> Temperature2mMin { get; set; } = [];

    [JsonPropertyName("weather_code")]
    public List<int> WeatherCode { get; set; } = [];
  }
}
