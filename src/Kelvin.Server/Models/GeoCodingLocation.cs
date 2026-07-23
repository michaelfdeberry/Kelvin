namespace Kelvin.Server.Models;

public class GeoCodingLocation
{
  public long Id { get; set; }

  public string Name { get; set; } = string.Empty;

  public double Latitude { get; set; }

  public double Longitude { get; set; }

  public double? Elevation { get; set; }

  public string? TimeZone { get; set; }

  public string? Country { get; set; }

  public string? CountryCode { get; set; }

  public string? Admin1 { get; set; }

  public string? Admin2 { get; set; }

  public string? Admin3 { get; set; }

  public IReadOnlyList<string> PostCodes { get; set; } = [];
}
