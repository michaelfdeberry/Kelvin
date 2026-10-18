using Kelvin.Protocol;
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
    return HmiCbor.Encode(
      5,
      writer =>
      {
        writer.WriteUInt32(1);
        writer.WriteUInt32((uint)change.State);
        writer.WriteUInt32(2);
        HmiCbor.WriteNullableFloat(writer, change.EnvironmentTemperatureC);
        writer.WriteUInt32(3);
        HmiCbor.WriteNullableFloat(writer, change.TargetTemperatureC);
        writer.WriteUInt32(4);
        HmiCbor.WriteNullableFloat(writer, change.HumidityPercentage);
        writer.WriteUInt32(5);
        HmiCbor.WriteNullableFloat(writer, change.CO2LevelPpm);
      }
    );
  }
}
