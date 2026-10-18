using Kelvin.Protocol;

namespace Kelvin.Server.Features.Hmi;

/// <summary>
/// Binary-encodes the full thermostat state for the panel; kept compact (vs JSON) since the target is an
/// embedded ESP32 decoding it over ESP-NOW, not a browser.
/// </summary>
public static class HmiThermostatStateEncoder
{
  public static byte[] Encode(Models.Thermostat thermostat)
  {
    var setPoints = thermostat.SetPoints.Where(setPoint => setPoint.DeletedAt is null).ToList();
    var schedules = thermostat.Schedules.Where(schedule => schedule.DeletedAt is null).ToList();
    ArgumentOutOfRangeException.ThrowIfGreaterThan(setPoints.Count, 2);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(schedules.Count, 32);

    return HmiCbor.Encode(
      7,
      writer =>
      {
        writer.WriteUInt32(1);
        writer.WriteUInt32((uint)thermostat.Mode);
        writer.WriteUInt32(2);
        writer.WriteBoolean(thermostat.FanEnabled);
        writer.WriteUInt32(3);
        HmiCbor.WriteFloat(writer, thermostat.HysteresisC);
        writer.WriteUInt32(4);
        HmiCbor.WriteNullableFloat(writer, thermostat.HeatingLockoutC);
        writer.WriteUInt32(5);
        HmiCbor.WriteNullableFloat(writer, thermostat.CoolingLockoutC);
        writer.WriteUInt32(6);
        writer.WriteStartArray(setPoints.Count);
        foreach (var setPoint in setPoints)
        {
          writer.WriteStartMap(2);
          writer.WriteUInt32(1);
          writer.WriteUInt32((uint)setPoint.Type);
          writer.WriteUInt32(2);
          HmiCbor.WriteFloat(writer, setPoint.TargetTemperatureC);
          writer.WriteEndMap();
        }
        writer.WriteEndArray();
        writer.WriteUInt32(7);
        writer.WriteStartArray(schedules.Count);
        foreach (var schedule in schedules)
        {
          writer.WriteStartMap(5);
          writer.WriteUInt32(1);
          writer.WriteByteString(schedule.Id.ToByteArray());
          writer.WriteUInt32(2);
          writer.WriteUInt32((uint)schedule.Type);
          writer.WriteUInt32(3);
          writer.WriteUInt32((uint)(schedule.StartTime.Hour * 60 + schedule.StartTime.Minute));
          writer.WriteUInt32(4);
          writer.WriteUInt32((uint)(schedule.EndTime.Hour * 60 + schedule.EndTime.Minute));
          writer.WriteUInt32(5);
          HmiCbor.WriteFloat(writer, schedule.TargetTemperatureC);
          writer.WriteEndMap();
        }
        writer.WriteEndArray();
      }
    );
  }
}
