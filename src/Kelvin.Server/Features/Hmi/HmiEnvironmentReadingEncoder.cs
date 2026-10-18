using System.Formats.Cbor;
using Kelvin.Server.Models;

namespace Kelvin.Server.Features.Hmi;

/// <summary>
/// CBOR-encodes the system-wide average reading using the versioned integer-keyed HMI payload contract.
/// </summary>
public static class HmiEnvironmentReadingEncoder
{
  public static byte[] Encode(EnvironmentReading reading)
  {
    if (!float.IsFinite(reading.TemperatureC) || !float.IsFinite(reading.HumidityPercentage) || !float.IsFinite(reading.CO2LevelPpm))
      throw new ArgumentOutOfRangeException(nameof(reading), "HMI readings must be finite.");

    var writer = new CborWriter(CborConformanceMode.Strict);
    writer.WriteStartMap(4);
    writer.WriteUInt32(0);
    writer.WriteUInt32(1);
    writer.WriteUInt32(1);
    writer.WriteSingle(reading.TemperatureC);
    writer.WriteUInt32(2);
    writer.WriteSingle(reading.HumidityPercentage);
    writer.WriteUInt32(3);
    writer.WriteSingle(reading.CO2LevelPpm);
    writer.WriteEndMap();
    return writer.Encode();
  }
}
