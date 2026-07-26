using System.Text.Json.Serialization;

namespace Kelvin.Server.Integration.GeoCoding;

public sealed partial class OpenMeteoGeoCodingApi
{
  private sealed class OpenMeteoGeoCodingLocationResponse
  {
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("elevation")]
    public double? Elevation { get; set; }

    [JsonPropertyName("timezone")]
    public string? TimeZone { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("country_code")]
    public string? CountryCode { get; set; }

    [JsonPropertyName("admin1")]
    public string? Admin1 { get; set; }

    [JsonPropertyName("admin2")]
    public string? Admin2 { get; set; }

    [JsonPropertyName("admin3")]
    public string? Admin3 { get; set; }

    [JsonPropertyName("feature_code")]
    public string? FeatureCode { get; set; }

    [JsonPropertyName("population")]
    public long? Population { get; set; }

    [JsonPropertyName("postcodes")]
    public List<string>? PostCodes { get; set; }
  }
}
