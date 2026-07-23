using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kelvin.Server.Models;

namespace Kelvin.Server.Integration;

public interface IGeoCodingApi
{
  Task<IReadOnlyList<GeoCodingLocation>> SearchAsync(string name, int count = 10, CancellationToken cancellationToken = default);

  Task<GeoCodingLocation?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class OpenMeteoGeoCodingApi(IHttpClientFactory httpClientFactory) : IGeoCodingApi
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

  private const string ClientName = "OpenMeteo";

  public async Task<IReadOnlyList<GeoCodingLocation>> SearchAsync(string name, int count = 10, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(name))
      return [];

    var httpClient = httpClientFactory.CreateClient(ClientName);
    var requestUri = $"search?name={Uri.EscapeDataString(name)}&count={count.ToString(CultureInfo.InvariantCulture)}&language=en&format=json";

    var response = await httpClient.GetFromJsonAsync<GeoCodingResponse>(requestUri, JsonOptions, cancellationToken);
    if (response?.Results is null || response.Results.Count == 0)
      return [];

    return response
      .Results.Where(location => !string.IsNullOrWhiteSpace(location.Name))
      .Select(location => new GeoCodingLocation
      {
        Id = location.Id,
        Name = location.Name,
        Latitude = location.Latitude,
        Longitude = location.Longitude,
        Elevation = location.Elevation,
        TimeZone = location.TimeZone,
        Country = location.Country,
        CountryCode = location.CountryCode,
        Admin1 = location.Admin1,
        Admin2 = location.Admin2,
        Admin3 = location.Admin3,
        PostCodes = location.PostCodes ?? [],
      })
      .ToArray();
  }

  public async Task<GeoCodingLocation?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
  {
    if (id <= 0)
      return null;

    var httpClient = httpClientFactory.CreateClient(ClientName);
    var requestUri = $"get?id={id.ToString(CultureInfo.InvariantCulture)}&format=json";

    var response = await httpClient.GetFromJsonAsync<GeoCodingLocationResponse>(requestUri, JsonOptions, cancellationToken);
    if (response is null || string.IsNullOrWhiteSpace(response.Name))
      return null;

    return new GeoCodingLocation
    {
      Id = response.Id,
      Name = response.Name,
      Latitude = response.Latitude,
      Longitude = response.Longitude,
      Elevation = response.Elevation,
      TimeZone = response.TimeZone,
      Country = response.Country,
      CountryCode = response.CountryCode,
      Admin1 = response.Admin1,
      Admin2 = response.Admin2,
      Admin3 = response.Admin3,
      PostCodes = response.PostCodes ?? [],
    };
  }

  private sealed class GeoCodingResponse
  {
    [JsonPropertyName("results")]
    public List<GeoCodingLocationResponse>? Results { get; set; }
  }

  private sealed class GeoCodingLocationResponse
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
