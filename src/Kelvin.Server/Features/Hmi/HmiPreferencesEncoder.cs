using Kelvin.Protocol;

namespace Kelvin.Server.Features.Hmi;

/// <summary>
/// Binary-encodes the system preferences.
/// </summary>
public static class HmiPreferencesEncoder
{
  public static byte[] Encode(Models.Preferences preferences)
  {
    return HmiCbor.Encode(
      2,
      writer =>
      {
        writer.WriteUInt32(1);
        writer.WriteUInt32((uint)preferences.TemperatureUnit);
        writer.WriteUInt32(2);
        writer.WriteUInt32((uint)preferences.TimeFormat);
      }
    );
  }
}
