using FakeItEasy;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Control;
using Kelvin.Server.Hubs;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Control;

public class EmergencyShutdownTests
{
    [Fact]
    public async Task DisablesThermostatAndPublishesShutdown()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        context.Thermostats.Add(
            new Models.Thermostat { Mode = RunMode.Automatic, FanEnabled = true }
        );
        await context.SaveChangesAsync();

        var controlChannel = A.Fake<IControlChannel>();
        var controlClient = A.Fake<IControlClient>();
        var controlClients = A.Fake<IHubClients<IControlClient>>();
        A.CallTo(() => controlClients.All).Returns(controlClient);
        A.CallTo(() => controlClient.ThermostatStateChanged()).Returns(Task.CompletedTask);
        var controlHub = A.Fake<IHubContext<ControlHub, IControlClient>>();
        A.CallTo(() => controlHub.Clients).Returns(controlClients);

        var notificationClient = A.Fake<INotificationsClient>();
        var notificationClients = A.Fake<IHubClients<INotificationsClient>>();
        A.CallTo(() => notificationClients.All).Returns(notificationClient);
        A.CallTo(() => notificationClient.Notify(A<Notification>._)).Returns(Task.CompletedTask);
        var notificationHub = A.Fake<IHubContext<NotificationsHub, INotificationsClient>>();
        A.CallTo(() => notificationHub.Clients).Returns(notificationClients);

        var handler = new EmergencyShutdownHandler(
            context,
            NullLogger<EmergencyShutdownHandler>.Instance,
            new MemoryCache(new MemoryCacheOptions()),
            controlChannel,
            controlHub,
            notificationHub
        );

        var result = await handler.HandleAsync(new EmergencyShutdownRequest("sensor failure"));

        result.IsSuccess.ShouldBeTrue();
        A.CallTo(() =>
                controlChannel.WriteAsync(
                    A<ControlMessage>.That.Matches(message =>
                        message.State == ControlState.Disable && message.Reason == "sensor failure"
                    ),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                notificationClient.Notify(
                    A<Notification>.That.Matches(notification =>
                        notification.Heading == "Emergency Shutdown"
                        && notification.Message == "sensor failure"
                    )
                )
            )
            .MustHaveHappenedOnceExactly();

        await using var readContext = harness.CreateContext();
        var thermostat = readContext.Thermostats.Single();
        thermostat.Mode.ShouldBe(RunMode.Disabled);
        thermostat.FanEnabled.ShouldBeFalse();
    }
}
