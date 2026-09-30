using Kelvin.Server.Application;
using Kelvin.Server.Data;
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
  IHandler<UpdateThermostatSettingsRequest> updateThermostatSettings
) : IHandler<ReceiveHmiCommandRequest>
{
  public async Task<Result> HandleAsync(ReceiveHmiCommandRequest request, CancellationToken ct = default)
  {
    await RegisterHmiAsync(request.MacAddress, ct);

    if (request.Payload.Length == 0)
      return Result.Failure(ReceiveHmiCommandErrors.EmptyPayload);

    var thermostat = await context.Thermostats.Include(t => t.SetPoints).Include(t => t.Schedules).FirstOrDefaultAsync(ct);
    if (thermostat is null)
      return Result.Failure(ReceiveHmiCommandErrors.ThermostatNotFound);

    var messageType = (HmiMessageType)request.Payload[0];
    var body = request.Payload.AsSpan(1);

    return messageType switch
    {
      HmiMessageType.SetMode => await updateThermostat.HandleAsync(new UpdateThermostatRequest((RunMode)body[0], thermostat.FanEnabled), ct),
      HmiMessageType.SetFanEnabled => await updateThermostat.HandleAsync(new UpdateThermostatRequest(thermostat.Mode, body[0] != 0), ct),
      HmiMessageType.SetForecastLockouts => await updateThermostatSettings.HandleAsync(BuildForecastLockoutsRequest(thermostat, body), ct),
      HmiMessageType.SetSetPoint => await updateThermostatSettings.HandleAsync(BuildSetPointRequest(thermostat, body), ct),
      HmiMessageType.UpsertSchedule => await updateThermostatSettings.HandleAsync(BuildUpsertScheduleRequest(thermostat, body), ct),
      HmiMessageType.RemoveSchedule => await updateThermostatSettings.HandleAsync(BuildRemoveScheduleRequest(thermostat, body), ct),
      _ => Result.Failure(ReceiveHmiCommandErrors.UnknownCommand),
    };
  }

  private async Task RegisterHmiAsync(string macAddress, CancellationToken ct)
  {
    var hmi = await context.Hmis.FirstOrDefaultAsync(h => h.MacAddress == macAddress, ct);
    if (hmi is null)
    {
      context.Hmis.Add(new Models.Hmi { MacAddress = macAddress, Enabled = true });
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
