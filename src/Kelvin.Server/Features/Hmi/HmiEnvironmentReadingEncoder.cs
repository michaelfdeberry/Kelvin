using Kelvin.Server.Models;

namespace Kelvin.Server.Features.Hmi;

/// <summary>
/// Binary-encodes the system-wide average <see cref="EnvironmentReading" /> for the panel's real-time
/// current-reading display. Fixed-size and small enough to never need chunking.
/// </summary>
public static class HmiEnvironmentReadingEncoder
{
  public static byte[] Encode(EnvironmentReading reading)
  {
    using var stream = new MemoryStream();
    stream.WriteByte((byte)HmiMessageType.EnvironmentReadingChanged);
    stream.Write(BitConverter.GetBytes(reading.TemperatureC));
    stream.Write(BitConverter.GetBytes(reading.HumidityPercentage));
    stream.Write(BitConverter.GetBytes(reading.CO2LevelPpm));
    return stream.ToArray();
  }
}
