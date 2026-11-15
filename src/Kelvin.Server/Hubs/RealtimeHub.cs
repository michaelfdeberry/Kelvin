using Kelvin.Server.Application;
using Kelvin.Server.Features.Devices;
using Microsoft.AspNetCore.SignalR;

namespace Kelvin.Server.Hubs;

/// <summary>
/// The single envelope every real-time push is wrapped in; <see cref="Type" /> discriminates the payload shape.
/// </summary>
public record RealtimeMessage(string Type, object? Payload);

/// <summary>The <see cref="RealtimeMessage.Type" /> values the client knows how to handle.</summary>
public static class RealtimeMessageTypes
{
  public const string ControlStateChanged = "ControlStateChanged";
  public const string ThermostatSettingsChanged = "ThermostatSettingsChanged";
  public const string SensorsSettingsChanged = "SensorsSettingsChanged";
  public const string DevicesSettingsChanged = "DevicesSettingsChanged";
  public const string DeviceStateChanged = "DeviceStateChanged";
  public const string EnvironmentReadingChanged = "EnvironmentReadingChanged";
  public const string NotificationSent = "NotificationSent";
  public const string NotificationDismissed = "NotificationDismissed";
  public const string PreferencesChanged = "PreferencesChanged";
}

public interface IRealtimeClient
{
  Task Receive(RealtimeMessage message);
}

/// <summary>
/// Pushes every server->client real-time update over a single connection so the UI does not have to poll, and
/// does not need a separate hub per concern.
/// </summary>
/// <remarks>
/// Broadcast only - clients subscribe and listen, there is nothing they can invoke. Anything that changes the
/// system goes through the API so it passes the same handlers and safety guards.
/// </remarks>
public class RealtimeHub(IHandler<GetDeviceStatesRequest, GetDeviceStatesResponse> deviceStates) : Hub<IRealtimeClient>
{
  // Device state is only broadcast on change, so a new connection would otherwise not see it until the next one.
  public override async Task OnConnectedAsync()
  {
    var result = await deviceStates.HandleAsync(new GetDeviceStatesRequest(), Context.ConnectionAborted);
    if (result.IsSuccess)
    {
      await Clients.Caller.Receive(new RealtimeMessage(RealtimeMessageTypes.DeviceStateChanged, result.Value!.States));
    }

    await base.OnConnectedAsync();
  }
}

public class RealtimeHubEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapHub<RealtimeHub>("/hubs/realtime");
  }
}
