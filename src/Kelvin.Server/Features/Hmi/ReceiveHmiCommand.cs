using Kelvin.Protocol;
using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Features.Notifications;
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
  public static readonly Error InvalidPayload = new("ReceiveHmiCommand.InvalidPayload", "The Hmi CBOR command payload was invalid.");
}

/// <summary>
/// Decodes a command relayed from a panel (see HmiMessageType) and applies it through the same handlers the
/// web client uses, so it gets the same validation/safety checks and pushes the same notifications.
/// </summary>
public class ReceiveHmiCommandHandler(
  KelvinContext context,
  IHandler<UpdateThermostatRequest> updateThermostat,
  IHandler<UpdateThermostatSettingsRequest> updateThermostatSettings,
  IHandler<SaveSensorPacketRequest> saveSensorPacket,
  IHandler<SendHmiStateRequest> sendHmiState,
  IHandler<DismissNotificationRequest> dismissNotification
) : IHandler<ReceiveHmiCommandRequest>
{
  public async Task<Result> HandleAsync(ReceiveHmiCommandRequest request, CancellationToken ct = default)
  {
    if (
      !DeviceEnvelope.TryDecode(request.Payload, out var type, out var chunkIndex, out var chunkCount, out _, out _)
      || chunkIndex != 0
      || chunkCount != 1
    )
      return Result.Failure(ReceiveHmiCommandErrors.EmptyPayload);

    var messageType = (HmiMessageType)type;

    IRequest? command;
    try
    {
      var body = HmiCborMap.Parse(DecodeBody(request.Payload));
      if (messageType == HmiMessageType.SensorReading)
      {
        command = new SaveSensorPacketRequest(BuildSensorPacket(request.MacAddress, body), DeviceType.Hmi);
      }
      else if (messageType == HmiMessageType.DismissNotification)
      {
        command = new DismissNotificationRequest(body.Id(1)!.Value);
      }
      else if (messageType == HmiMessageType.RequestState)
      {
        command = new SendHmiStateRequest(request.MacAddress);
      }
      else
      {
        var thermostat = await context.Thermostats.Include(t => t.SetPoints).Include(t => t.Schedules).FirstOrDefaultAsync(ct);
        if (thermostat is null)
          return Result.Failure(ReceiveHmiCommandErrors.ThermostatNotFound);
        command = messageType switch
        {
          HmiMessageType.SetMode => new UpdateThermostatRequest((RunMode)body.UInt(1, 4), thermostat.FanEnabled),
          HmiMessageType.SetFanEnabled => new UpdateThermostatRequest(thermostat.Mode, body.Boolean(1)),
          HmiMessageType.SetForecastLockouts => BuildForecastLockoutsRequest(thermostat, body),
          HmiMessageType.SetSetPoint => BuildSetPointRequest(thermostat, body),
          HmiMessageType.UpsertSchedule => BuildUpsertScheduleRequest(thermostat, body),
          HmiMessageType.RemoveSchedule => BuildRemoveScheduleRequest(thermostat, body),
          _ => null,
        };
      }
    }
    catch (Exception exception) when (HmiCbor.IsDecodeError(exception))
    {
      return Result.Failure(ReceiveHmiCommandErrors.InvalidPayload);
    }
    if (command is null)
      return Result.Failure(ReceiveHmiCommandErrors.UnknownCommand);
    await RegisterHmiAsync(request.MacAddress, ct);
    return command switch
    {
      SaveSensorPacketRequest reading => await saveSensorPacket.HandleAsync(reading, ct),
      SendHmiStateRequest snapshot => await sendHmiState.HandleAsync(snapshot, ct),
      DismissNotificationRequest dismiss => await dismissNotification.HandleAsync(dismiss, ct),
      UpdateThermostatRequest update => await updateThermostat.HandleAsync(update, ct),
      UpdateThermostatSettingsRequest settings => await updateThermostatSettings.HandleAsync(settings, ct),
      _ => Result.Failure(ReceiveHmiCommandErrors.UnknownCommand),
    };
  }

  private static byte[] DecodeBody(byte[] payload) =>
    DeviceEnvelope.TryDecode(payload, out _, out _, out _, out _, out var body) ? body.ToArray() : [];

  private static SensorPacket BuildSensorPacket(string macAddress, HmiCborMap body) =>
    new()
    {
      MacAddress = macAddress,
      TemperatureC = body.Float(1),
      HumidityPercentage = body.Float(2),
      CO2LevelPpm = (ushort)body.UInt(3, ushort.MaxValue),
      BatteryLevelPercentage = body.Float(4),
    };

  private async Task RegisterHmiAsync(string macAddress, CancellationToken ct)
  {
    var hmi = await context.Hmis.Include(h => h.Sensor).FirstOrDefaultAsync(h => h.MacAddress == macAddress, ct);
    if (hmi is null)
    {
      // The panel's own reading may have already registered a Sensor for this MAC before its first command
      // arrived here - link to it rather than leaving SensorId null until a later reading creates one.
      var sensor = await context.Sensors.FirstOrDefaultAsync(s => s.MacAddress == macAddress, ct);
      context.Hmis.Add(new Models.Hmi { MacAddress = macAddress, SensorId = sensor?.Id });
      await context.SaveChangesAsync(ct);
      return;
    }

    // if it was deleted, but starts sending commands again restore it, but leave it disabled - mirrors SaveSensorPacketHandler.
    if (hmi.DeletedAt is not null)
    {
      if (hmi.Sensor is not null)
        hmi.Sensor.Enabled = false;
      hmi.DeletedAt = null;
      await context.SaveChangesAsync(ct);
    }
  }

  private static UpdateThermostatSettingsRequest BuildForecastLockoutsRequest(Models.Thermostat thermostat, HmiCborMap body)
  {
    var heatingLockoutC = body.NullableFloat(1);
    var coolingLockoutC = body.NullableFloat(2);

    return BuildSettingsRequest(thermostat, heatingLockoutC, coolingLockoutC, ToSetPointInputs(thermostat), ToScheduleInputs(thermostat));
  }

  private static UpdateThermostatSettingsRequest BuildSetPointRequest(Models.Thermostat thermostat, HmiCborMap body)
  {
    var type = (RunType)body.UInt(1, 1);
    var targetTemperatureC = body.Float(2);
    var existingId = thermostat.SetPoints.Where(setPoint => setPoint.DeletedAt is null).FirstOrDefault(setPoint => setPoint.Type == type)?.Id;

    var setPoints = ToSetPointInputs(thermostat)
      .Where(setPoint => setPoint.Type != type)
      .Append(new SetPointInput(existingId, type, targetTemperatureC));

    return BuildSettingsRequest(thermostat, thermostat.HeatingLockoutC, thermostat.CoolingLockoutC, setPoints, ToScheduleInputs(thermostat));
  }

  private static UpdateThermostatSettingsRequest BuildUpsertScheduleRequest(Models.Thermostat thermostat, HmiCborMap body)
  {
    var id = body.Id(1, true);
    var type = (RunType)body.UInt(2, 1);
    var startMinutes = body.UInt(3, 1439);
    var endMinutes = body.UInt(4, 1439);
    var targetTemperatureC = body.Float(5);

    var startTime = new TimeOnly((int)startMinutes / 60, (int)startMinutes % 60);
    var endTime = new TimeOnly((int)endMinutes / 60, (int)endMinutes % 60);

    var schedules = ToScheduleInputs(thermostat)
      .Where(schedule => schedule.Id != id)
      .Append(new ScheduleInput(id, type, startTime, endTime, targetTemperatureC));

    return BuildSettingsRequest(thermostat, thermostat.HeatingLockoutC, thermostat.CoolingLockoutC, ToSetPointInputs(thermostat), schedules);
  }

  private static UpdateThermostatSettingsRequest BuildRemoveScheduleRequest(Models.Thermostat thermostat, HmiCborMap body)
  {
    var id = body.Id(1);
    var schedules = ToScheduleInputs(thermostat).Where(schedule => schedule.Id != id);

    return BuildSettingsRequest(thermostat, thermostat.HeatingLockoutC, thermostat.CoolingLockoutC, ToSetPointInputs(thermostat), schedules);
  }

  private static UpdateThermostatSettingsRequest BuildSettingsRequest(
    Models.Thermostat thermostat,
    float? heatingLockoutC,
    float? coolingLockoutC,
    IEnumerable<SetPointInput> setPoints,
    IEnumerable<ScheduleInput> schedules
  ) =>
    new(
      heatingLockoutC,
      coolingLockoutC,
      setPoints.Where(setPoint => IsModeAvailable(thermostat.Mode, setPoint.Type)).ToArray(),
      schedules.Where(schedule => IsModeAvailable(thermostat.Mode, schedule.Type)).ToArray(),
      TargetMode: thermostat.Mode
    );

  private static bool IsModeAvailable(RunMode mode, RunType type) =>
    mode == RunMode.Automatic || mode == (type == RunType.Heating ? RunMode.Heating : RunMode.Cooling);

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
