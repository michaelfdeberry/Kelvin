using FakeItEasy;
using Kelvin.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Kelvin.Server.Tests.Features.Sensors;

internal static class SensorTestHelpers
{
    public static (IHubContext<ControlHub, IControlClient> Hub, IControlClient Client) CreateControlHub()
    {
        var client = A.Fake<IControlClient>();
        var clients = A.Fake<IHubClients<IControlClient>>();
        A.CallTo(() => clients.All).Returns(client);
        A.CallTo(() => client.SensorsStateChanged()).Returns(Task.CompletedTask);
        var hub = A.Fake<IHubContext<ControlHub, IControlClient>>();
        A.CallTo(() => hub.Clients).Returns(clients);
        return (hub, client);
    }
}