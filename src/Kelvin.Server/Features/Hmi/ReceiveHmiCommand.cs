using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Hmi;

public record ReceiveHmiCommandRequest(string MacAddress, byte[] Payload) : IRequest;

public static class ReceiveHmiCommandErrors
{
  public static readonly Error EmptyPayload = new("ReceiveHmiCommand.EmptyPayload", "The Hmi command payload was empty.");
  public static readonly Error UnknownCommand = new("ReceiveHmiCommand.UnknownCommand", "The Hmi command type was not recognized.");
  public static readonly Error ThermostatNotFound = new("ReceiveHmiCommand.ThermostatNotFound", "The thermostat has not been configured yet.");
}

/// <summary>
/// Decodes a command relayed from a panel (see HmiMessageType) and applies it through the same handlers the
/// web client uses, so it gets the same validation/safety checks and pushes the same notifications.
/// </summary>
public class ReceiveHmiCommandHandler(
  KelvinContext context,
  IHandler<UpdateThermostatRequest> updateThermostat,
  IHandler<UpdateThermostatSettingsRequest> updateThermostatSettings,
  IHandler<SaveSensorPacketRequest> saveSensorPacket
) : IHandler<ReceiveHmiCommandRequest>
{
  public async Task<Result> HandleAsync(ReceiveHmiCommandRequest request, CancellationToken ct = default)
  {
    await RegisterHmiAsync(request.MacAddress, ct);

    if (!HmiEnvelope.TryDecode(request.Payload, out var messageType, out _, out _, out _))
      return Result.Failure(ReceiveHmiCommandErrors.EmptyPayload);

    if (messageType == HmiMessageType.SensorReading)
    {
      // A ReadOnlySpan<byte> can't be kept as a local across an `await`, so it's re-decoded at each use
      // site below instead of stored once in a shared variable.
      var packet = BuildSensorPacket(request.MacAddress, DecodeBody(request.Payload));
      return await saveSensorPacket.HandleAsync(new SaveSensorPacketRequest(packet, DeviceType.Hmi), ct);
    }

    var thermostat = await context.Thermostats.Include(t => t.SetPoints).Include(t => t.Schedules).FirstOrDefaultAsync(ct);
    if (thermostat is null)
      return Result.Failure(ReceiveHmiCommandErrors.ThermostatNotFound);

    return messageType switch
    {
      HmiMessageType.SetMode => await updateThermostat.HandleAsync(
        new UpdateThermostatRequest((RunMode)DecodeBody(request.Payload)[0], thermostat.FanEnabled),
        ct
      ),
      HmiMessageType.SetFanEnabled => await updateThermostat.HandleAsync(
        new UpdateThermostatRequest(thermostat.Mode, DecodeBody(request.Payload)[0] != 0),
        ct
      ),
      HmiMessageType.SetForecastLockouts => await updateThermostatSettings.HandleAsync(
        BuildForecastLockoutsRequest(thermostat, DecodeBody(request.Payload)),
        ct
      ),
      HmiMessageType.SetSetPoint => await updateThermostatSettings.HandleAsync(BuildSetPointRequest(thermostat, DecodeBody(request.Payload)), ct),
      HmiMessageType.UpsertSchedule => await updateThermostatSettings.HandleAsync(
        BuildUpsertScheduleRequest(thermostat, DecodeBody(request.Payload)),
        ct
      ),
      HmiMessageType.RemoveSchedule => await updateThermostatSettings.HandleAsync(
        BuildRemoveScheduleRequest(thermostat, DecodeBody(request.Payload)),
        ct
      ),
      _ => Result.Failure(ReceiveHmiCommandErrors.UnknownCommand),
    };
  }

  private static ReadOnlySpan<byte> DecodeBody(byte[] payload) => HmiEnvelope.TryDecode(payload, out _, out _, out _, out var body) ? body : default;

  private static SensorPacket BuildSensorPacket(string macAddress, ReadOnlySpan<byte> body) =>
    new()
    {
      MacAddress = macAddress,
      TemperatureC = BitConverter.ToSingle(body),
      HumidityPercentage = BitConverter.ToSingle(body[4..]),
      CO2LevelPpm = BitConverter.ToUInt16(body[8..]),
      BatteryLevelPercentage = BitConverter.ToSingle(body[10..]),
    };

  private async Task RegisterHmiAsync(string macAddress, CancellationToken ct)
  {
    var hmi = await context.Hmis.FirstOrDefaultAsync(h => h.MacAddress == macAddress, ct);
    if (hmi is null)
    {
      // The panel's own reading may have already registered a Sensor for this MAC before its first command
      // arrived here - link to it rather than leaving SensorId null until a later reading creates one.
      var sensor = await context.Sensors.FirstOrDefaultAsync(s => s.MacAddress == macAddress, ct);
      context.Hmis.Add(
        new Models.Hmi
        {
          MacAddress = macAddress,
          Enabled = true,
          SensorId = sensor?.Id,
        }
      );
      await context.SaveChangesAsync(ct);
      return;
    }

    // if it was deleted, but starts sending commands again restore it, but leave it disabled - mirrors SaveSensorPacketHandler.
    if (hmi.DeletedAt is not null)
    {
      hmi.Enabled = false;
      hmi.DeletedAt = null;
      await context.SaveChangesAsync(ct);
    }
  }

  private static UpdateThermostatSettingsRequest BuildForecastLockoutsRequest(Models.Thermostat thermostat, ReadOnlySpan<byte> body)
  {
    var offset = 0;
    var heatingLockoutC = ReadOptionalFloat(body, ref offset);
    var coolingLockoutC = ReadOptionalFloat(body, ref offset);

    return new UpdateThermostatSettingsRequest(heatingLockoutC, coolingLockoutC, ToSetPointInputs(thermostat), ToScheduleInputs(thermostat));
  }

  private static UpdateThermostatSettingsRequest BuildSetPointRequest(Models.Thermostat thermostat, ReadOnlySpan<byte> body)
  {
    var type = (RunType)body[0];
    var targetTemperatureC = BitConverter.ToSingle(body[1..]);
    var existingId = thermostat.SetPoints.Where(setPoint => setPoint.DeletedAt is null).FirstOrDefault(setPoint => setPoint.Type == type)?.Id;

    var setPoints = ToSetPointInputs(thermostat)
      .Where(setPoint => setPoint.Type != type)
      .Append(new SetPointInput(existingId, type, targetTemperatureC));

    return new UpdateThermostatSettingsRequest(thermostat.HeatingLockoutC, thermostat.CoolingLockoutC, setPoints, ToScheduleInputs(thermostat));
  }

  private static UpdateThermostatSettingsRequest BuildUpsertScheduleRequest(Models.Thermostat thermostat, ReadOnlySpan<byte> body)
  {
    var offset = 0;
    var hasId = body[offset++] != 0;
    Guid? id = null;
    if (hasId)
    {
      id = new Guid(body.Slice(offset, 16));
      offset += 16;
    }

    var type = (RunType)body[offset++];
    var startMinutes = BitConverter.ToUInt16(body[offset..]);
    offset += 2;
    var endMinutes = BitConverter.ToUInt16(body[offset..]);
    offset += 2;
    var targetTemperatureC = BitConverter.ToSingle(body[offset..]);

    var startTime = new TimeOnly(startMinutes / 60, startMinutes % 60);
    var endTime = new TimeOnly(endMinutes / 60, endMinutes % 60);

    var schedules = ToScheduleInputs(thermostat)
      .Where(schedule => schedule.Id != id)
      .Append(new ScheduleInput(id, type, startTime, endTime, targetTemperatureC));

    return new UpdateThermostatSettingsRequest(thermostat.HeatingLockoutC, thermostat.CoolingLockoutC, ToSetPointInputs(thermostat), schedules);
  }

  private static UpdateThermostatSettingsRequest BuildRemoveScheduleRequest(Models.Thermostat thermostat, ReadOnlySpan<byte> body)
  {
    var id = new Guid(body[..16]);
    var schedules = ToScheduleInputs(thermostat).Where(schedule => schedule.Id != id);

    return new UpdateThermostatSettingsRequest(thermostat.HeatingLockoutC, thermostat.CoolingLockoutC, ToSetPointInputs(thermostat), schedules);
  }

  private static float? ReadOptionalFloat(ReadOnlySpan<byte> body, ref int offset)
  {
    var hasValue = body[offset++] != 0;
    if (!hasValue)
      return null;

    var value = BitConverter.ToSingle(body[offset..]);
    offset += 4;
    return value;
  }

  private static IEnumerable<SetPointInput> ToSetPointInputs(Models.Thermostat thermostat) =>
    thermostat
      .SetPoints.Where(setPoint => setPoint.DeletedAt is null)
      .Select(setPoint => new SetPointInput(setPoint.Id, setPoint.Type, setPoint.TargetTemperatureC));

  private static IEnumerable<ScheduleInput> ToScheduleInputs(Models.Thermostat thermostat) =>
    thermostat
      .Schedules.Where(schedule => schedule.DeletedAt is null)
      .Select(schedule => new ScheduleInput(schedule.Id, schedule.Type, schedule.StartTime, schedule.EndTime, schedule.TargetTemperatureC));
}

public class ReceiveHmiCommandFeature : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<ReceiveHmiCommandRequest>, ReceiveHmiCommandHandler>();
  }
}
