using System.Globalization;
using System.Text.Json;
using Kelvin.Server.Models;

namespace Kelvin.Server.Integration.GeoCoding;

public sealed partial class OpenMeteoGeoCodingApi(IHttpClientFactory httpClientFactory) : IGeoCodingApi
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

  private const string CLIENT_NAME = "OpenMeteo";

  public async Task<IReadOnlyList<GeoCodingLocation>> SearchAsync(string name, int count = 10, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(name))
      return [];

    var httpClient = httpClientFactory.CreateClient(CLIENT_NAME);
    var requestUri = $"search?name={Uri.EscapeDataString(name)}&count={count.ToString(CultureInfo.InvariantCulture)}&language=en&format=json";

    var response = await httpClient.GetFromJsonAsync<OpenMeteoGeoCodingResponse>(requestUri, JsonOptions, cancellationToken);
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

    var httpClient = httpClientFactory.CreateClient(CLIENT_NAME);
    var requestUri = $"get?id={id.ToString(CultureInfo.InvariantCulture)}&format=json";

    var response = await httpClient.GetFromJsonAsync<OpenMeteoGeoCodingLocationResponse>(requestUri, JsonOptions, cancellationToken);
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
}
