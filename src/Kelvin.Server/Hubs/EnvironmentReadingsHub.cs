using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Models;
using Microsoft.AspNetCore.SignalR;

namespace Kelvin.Server.Hubs;

public interface IEnvironmentReadingsClient
{
  Task ReadingsUpdated(EnvironmentReading reading);
}

public class EnvironmentReadingsHub(ISensorPacketChannel sensorPacketChannel) : Hub<IEnvironmentReadingsClient>
{
  public async Task SubmitReading(SensorPacket reading)
  {
    await sensorPacketChannel.WriteAsync(reading, Context.ConnectionAborted);
  }
}

public class EnvironmentReadingsHubMapper : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapHub<EnvironmentReadingsHub>("/hubs/readings");
  }
}
