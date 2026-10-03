using Kelvin.Server.Application;
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
  public const string ThermostatStateChanged = "ThermostatStateChanged";
  public const string SensorsStateChanged = "SensorsStateChanged";
  public const string ReadingsUpdated = "ReadingsUpdated";
  public const string Notify = "Notify";
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
public class RealtimeHub : Hub<IRealtimeClient> { }

public class RealtimeHubEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapHub<RealtimeHub>("/hubs/realtime");
  }
}
