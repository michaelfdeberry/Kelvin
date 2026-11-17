using Kelvin.Server.Application;
using Kelvin.Server.Features.Control;
using Kelvin.Server.Features.Devices;
using Kelvin.Server.Features.Gateways;
using Kelvin.Server.Features.Humidifiers;
using Kelvin.Server.Features.Preferences;
using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Features.Weather;

namespace Kelvin.Server.Hubs;

/// <summary>
/// Reads the current value of everything the realtime hub pushes, reusing the query handlers the API serves.
/// </summary>
/// <remarks>
/// Used for the full push when a client connects; after that, each change is pushed with its own payload.
/// A snapshot that can't be read is skipped rather than failing the rest.
/// </remarks>
public class RealtimeSnapshots(
  IHandler<GetControlStateRequest, GetControlStateResponse> controlState,
  IHandler<GetLatestReadingsRequest, GetLatestReadingsResponse> latestReadings,
  IHandler<GetThermostatRequest, GetThermostatResponse> thermostat,
  IHandler<GetSetPointsRequest, GetSetPointsResponse> setPoints,
  IHandler<GetSchedulesRequest, GetSchedulesResponse> schedules,
  IHandler<GetSensorsRequest, GetSensorsResponse> sensors,
  IHandler<GetDevicesRequest, GetDevicesResponse> devices,
  IHandler<GetHumidifiersRequest, GetHumidifiersResponse> humidifiers,
  IHandler<GetDeviceStatesRequest, GetDeviceStatesResponse> deviceStates,
  IHandler<GetRelayStatesRequest, GetRelayStatesResponse> relayStates,
  IHandler<GetWeatherForecastRequest, GetWeatherForecastResponse> weather,
  IHandler<GetPreferencesRequest, GetPreferencesResponse> preferences
)
{
  /// <summary>Every snapshot, for a client that has just connected.</summary>
  public async Task<IReadOnlyList<RealtimeMessage>> AllAsync(CancellationToken ct = default)
  {
    var messages = new[]
    {
      await ControlStateAsync(ct),
      await EnvironmentReadingAsync(ct),
      await ThermostatAsync(ct),
      await SensorsAsync(ct),
      await DevicesAsync(ct),
      await DeviceStatesAsync(ct),
      await RelayStatesAsync(ct),
      await WeatherAsync(ct),
      await PreferencesAsync(ct),
    };

    return [.. messages.OfType<RealtimeMessage>()];
  }

  // Until anything has been actuated there is no call to report, so the client keeps its defaults.
  private async Task<RealtimeMessage?> ControlStateAsync(CancellationToken ct = default)
  {
    var result = await controlState.HandleAsync(new GetControlStateRequest(), ct);
    return result.IsSuccess && result.Value!.LastChange is { } change ? new RealtimeMessage(RealtimeMessageTypes.ControlStateChanged, change) : null;
  }

  private async Task<RealtimeMessage?> EnvironmentReadingAsync(CancellationToken ct = default)
  {
    var result = await latestReadings.HandleAsync(new GetLatestReadingsRequest(), ct);
    return result.IsSuccess ? new RealtimeMessage(RealtimeMessageTypes.EnvironmentReadingChanged, result.Value!.Reading) : null;
  }

  private async Task<RealtimeMessage?> ThermostatAsync(CancellationToken ct = default)
  {
    var thermostatResult = await thermostat.HandleAsync(new GetThermostatRequest(), ct);
    var setPointsResult = await setPoints.HandleAsync(new GetSetPointsRequest(), ct);
    var schedulesResult = await schedules.HandleAsync(new GetSchedulesRequest(), ct);
    if (thermostatResult.IsFailure || setPointsResult.IsFailure || schedulesResult.IsFailure)
      return null;

    var payload = new ThermostatSettingsPayload(
      GetThermostatResponseDto.FromThermostat(thermostatResult.Value!.Thermostat),
      setPointsResult.Value!.SetPoints,
      schedulesResult.Value!.Schedules
    );
    return new RealtimeMessage(RealtimeMessageTypes.ThermostatSettingsChanged, payload);
  }

  private async Task<RealtimeMessage?> SensorsAsync(CancellationToken ct = default)
  {
    var result = await sensors.HandleAsync(new GetSensorsRequest(), ct);
    return result.IsSuccess ? new RealtimeMessage(RealtimeMessageTypes.SensorsSettingsChanged, result.Value!.Sensors) : null;
  }

  private async Task<RealtimeMessage?> DevicesAsync(CancellationToken ct = default)
  {
    var devicesResult = await devices.HandleAsync(new GetDevicesRequest(), ct);
    var humidifiersResult = await humidifiers.HandleAsync(new GetHumidifiersRequest(), ct);
    if (devicesResult.IsFailure || humidifiersResult.IsFailure)
      return null;

    var payload = new DevicesSettingsPayload(devicesResult.Value!.Devices, humidifiersResult.Value!.Humidifiers);
    return new RealtimeMessage(RealtimeMessageTypes.DevicesSettingsChanged, payload);
  }

  private async Task<RealtimeMessage?> DeviceStatesAsync(CancellationToken ct = default)
  {
    var result = await deviceStates.HandleAsync(new GetDeviceStatesRequest(), ct);
    return result.IsSuccess ? new RealtimeMessage(RealtimeMessageTypes.DeviceStateChanged, result.Value!.States) : null;
  }

  private async Task<RealtimeMessage?> RelayStatesAsync(CancellationToken ct = default)
  {
    var result = await relayStates.HandleAsync(new GetRelayStatesRequest(), ct);
    return result.IsSuccess ? new RealtimeMessage(RealtimeMessageTypes.RelayStatesChanged, result.Value) : null;
  }

  // Cached only, so connecting never waits on the weather service; the refresh service keeps the cache warm and pushes what it fetches.
  private async Task<RealtimeMessage?> WeatherAsync(CancellationToken ct = default)
  {
    var result = await weather.HandleAsync(new GetWeatherForecastRequest(CachedOnly: true), ct);
    return result.IsSuccess ? new RealtimeMessage(RealtimeMessageTypes.WeatherForecastChanged, result.Value) : null;
  }

  private async Task<RealtimeMessage?> PreferencesAsync(CancellationToken ct = default)
  {
    var result = await preferences.HandleAsync(new GetPreferencesRequest(), ct);
    return result.IsSuccess ? new RealtimeMessage(RealtimeMessageTypes.PreferencesChanged, result.Value) : null;
  }
}

public class RealtimeSnapshotsRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<RealtimeSnapshots>();
  }
}
