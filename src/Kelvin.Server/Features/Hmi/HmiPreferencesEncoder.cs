namespace Kelvin.Server.Features.Hmi;

/// <summary>
/// Binary-encodes the system preferences.
/// </summary>
public static class HmiPreferencesEncoder
{
  public static byte[] Encode(Models.Preferences preferences)
  {
    using var stream = new MemoryStream();
    stream.WriteByte((byte)preferences.TemperatureUnit);
    stream.WriteByte((byte)preferences.TimeFormat);
    return stream.ToArray();
  }
}
