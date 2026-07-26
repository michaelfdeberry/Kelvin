using Kelvin.Server.Application;
using Kelvin.Server.Features.Control;
using Microsoft.AspNetCore.SignalR;

namespace Kelvin.Server.Hubs;

/// <summary>
/// The methods the server invokes on connected control clients.
/// </summary>
public interface IControlClient
{
  /// <summary>Raised every time the control service actuates a relay.</summary>
  Task ControlStateChanged(ControlStateChangeDto change);
}

/// <summary>
/// Pushes control state changes to connected clients so the UI does not have to poll for the current state.
/// </summary>
/// <remarks>
/// Broadcast only - clients subscribe and listen, there is nothing they can invoke. Anything that changes the
/// system goes through the API so it passes the same handlers and safety guards.
/// </remarks>
public class ControlHub : Hub<IControlClient> { }

public class ControlHubEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapHub<ControlHub>("/hubs/control");
  }
}
