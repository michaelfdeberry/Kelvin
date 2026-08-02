using Kelvin.Server.Application;
using Kelvin.Server.Models;
using Microsoft.AspNetCore.SignalR;

namespace Kelvin.Server.Hubs;

public interface IEnvironmentReadingsClient
{
  Task ReadingsUpdated(EnvironmentReading reading);
}

public class EnvironmentReadingsHub : Hub<IEnvironmentReadingsClient> { }

public class EnvironmentReadingsHubMapper : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapHub<EnvironmentReadingsHub>("/hubs/readings");
  }
}
