using Kelvin.Server.Application;
using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Models;
using Microsoft.AspNetCore.SignalR;

namespace Kelvin.Server.Hubs;

public interface IEnvironmentReadingsClient
{
  Task AcknowledgeReadingAsync(string? message = null);
  Task ReadingsUpdatedAsync(EnvironmentReading reading);
}

public class EnvironmentReadingsHub(ILogger<EnvironmentReadingsHub> logger, IDispatcher dispatcher) : Hub<IEnvironmentReadingsClient>
{
  public async Task SubmitReading(SensorPacket packet, CancellationToken cancellationToken)
  {
    logger.LogInformation("Received sensor packet from {MacAddress}", packet.MacAddress);
    await dispatcher.DispatchAsync(new SaveSensorPacketRequest(packet), cancellationToken);
    await Clients.Caller.AcknowledgeReadingAsync("Reading received successfully.");
  }
}

public class EnvironmentReadingsHubMapper : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapHub<EnvironmentReadingsHub>("/hubs/readings");
  }
}
