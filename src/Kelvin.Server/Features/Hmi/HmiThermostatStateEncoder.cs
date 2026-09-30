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

    using var stream = new MemoryStream();
    stream.WriteByte((byte)thermostat.Mode);
    stream.WriteByte((byte)(thermostat.FanEnabled ? 1 : 0));
    stream.Write(BitConverter.GetBytes(thermostat.HysteresisC));
    WriteOptionalFloat(stream, thermostat.HeatingLockoutC);
    WriteOptionalFloat(stream, thermostat.CoolingLockoutC);

    stream.WriteByte((byte)setPoints.Count);
    foreach (var setPoint in setPoints)
    {
      stream.WriteByte((byte)setPoint.Type);
      stream.Write(BitConverter.GetBytes(setPoint.TargetTemperatureC));
    }

    stream.WriteByte((byte)schedules.Count);
    foreach (var schedule in schedules)
    {
      // The Id is carried opaquely - the panel just echoes it back verbatim on Upsert/RemoveSchedule.
      stream.Write(schedule.Id.ToByteArray());
      stream.WriteByte((byte)schedule.Type);
      stream.Write(BitConverter.GetBytes((ushort)(schedule.StartTime.Hour * 60 + schedule.StartTime.Minute)));
      stream.Write(BitConverter.GetBytes((ushort)(schedule.EndTime.Hour * 60 + schedule.EndTime.Minute)));
      stream.Write(BitConverter.GetBytes(schedule.TargetTemperatureC));
    }

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
