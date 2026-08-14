using Kelvin.Server.Application;
using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Models;
using Microsoft.AspNetCore.SignalR;

namespace Kelvin.Server.Hubs;

public interface IEnvironmentReadingsClient
{
  Task ReadingsUpdated(EnvironmentReading reading);
}

public class EnvironmentReadingsHub(IDispatcher dispatcher) : Hub<IEnvironmentReadingsClient>
{
  public async Task SubmitReading(SensorPacket packet, CancellationToken cancellationToken)
  {
    await dispatcher.DispatchAsync(new SaveSensorPacketRequest(packet), cancellationToken);
  }
}

public class EnvironmentReadingsHubMapper : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapHub<EnvironmentReadingsHub>("/hubs/readings");
  }
}
