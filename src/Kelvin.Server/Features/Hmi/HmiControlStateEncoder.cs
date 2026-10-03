using Kelvin.Server.Models;

namespace Kelvin.Server.Features.Hmi;

/// <summary>
/// Binary-encodes a live Call-kind <see cref="ControlStateChange" /> for the panel's HEATING/COOLING badge.
/// Small and fixed-size enough to never need chunking, unlike <see cref="HmiThermostatStateEncoder" />.
/// </summary>
public static class HmiControlStateEncoder
{
  public static byte[] Encode(ControlStateChange change)
  {
    using var stream = new MemoryStream();
    stream.WriteByte((byte)HmiMessageType.ControlStateChanged);
    stream.WriteByte((byte)change.State);
    WriteOptionalFloat(stream, change.EnvironmentTemperatureC);
    WriteOptionalFloat(stream, change.TargetTemperatureC);
    WriteOptionalFloat(stream, change.HumidityPercentage);
    WriteOptionalFloat(stream, change.CO2LevelPpm);
    return stream.ToArray();
  }

  private static void WriteOptionalFloat(MemoryStream stream, float? value)
  {
    stream.WriteByte((byte)(value.HasValue ? 1 : 0));
    if (value.HasValue)
    {
      stream.Write(BitConverter.GetBytes(value.Value));
    }
  }
}
