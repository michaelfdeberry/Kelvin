using System.Text.Json.Serialization;

namespace Kelvin.Server.Integration.GeoCoding;

public sealed partial class OpenMeteoGeoCodingApi
{
  private sealed class OpenMeteoGeoCodingResponse
  {
    [JsonPropertyName("results")]
    public List<OpenMeteoGeoCodingLocationResponse>? Results { get; set; }
  }
}
