using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Data;
using Kelvin.Server.Features.Preferences;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Hmi;

public record SendHmiStateRequest(string MacAddress) : IRequest;

/// <summary>
/// Replies to a single panel's state request with thermostat settings, call state and preferences.
/// </summary>
public class SendHmiStateHandler(
  KelvinContext context,
  IHmiOutboundChannel outboundChannel,
  ILogger<SendHmiStateHandler> logger,
  IHandler<GetThermostatRequest, GetThermostatResponse> getThermostat,
  IHandler<GetPreferencesRequest, GetPreferencesResponse> getPreferences
) : IHandler<SendHmiStateRequest>
{
  public async Task<Result> HandleAsync(SendHmiStateRequest request, CancellationToken ct = default)
  {
    var thermostat = await getThermostat.HandleAsync(new GetThermostatRequest(), ct);
    if (thermostat.IsFailure)
      return Result.Failure(thermostat.Error);

    var preferences = await getPreferences.HandleAsync(new GetPreferencesRequest(), ct);
    if (preferences.IsFailure)
      return Result.Failure(preferences.Error);

    var lastCall = await context
      .ControlStateChanges.AsNoTracking()
      .Where(change => change.Kind == ControlChangeKind.Call && change.DeletedAt == null)
      .OrderByDescending(change => change.CreatedAt)
      .FirstOrDefaultAsync(ct);

    lastCall ??= new ControlStateChange { Kind = ControlChangeKind.Call, State = ControlState.Dwell };

    logger.LogInformation("Sending state snapshot to panel with MAC address {MacAddress}", request.MacAddress);
    WriteFrames(request.MacAddress, HmiMessageType.ControlStateChanged, HmiControlStateEncoder.Encode(lastCall));
    WriteFrames(
      request.MacAddress,
      HmiMessageType.PreferencesChanged,
      HmiPreferencesEncoder.Encode(
        new Models.Preferences { TemperatureUnit = preferences.Value!.TemperatureUnit, TimeFormat = preferences.Value.TimeFormat }
      )
    );
    WriteFrames(request.MacAddress, HmiMessageType.ThermostatStateChunk, HmiThermostatStateEncoder.Encode(thermostat.Value!.Thermostat));

    return Result.Success();
  }

  private void WriteFrames(string macAddress, HmiMessageType messageType, byte[] payload)
  {
    outboundChannel.Write(macAddress, messageType, payload);
  }
}

public class SendHmiStateRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<SendHmiStateRequest>, SendHmiStateHandler>();
  }
}
