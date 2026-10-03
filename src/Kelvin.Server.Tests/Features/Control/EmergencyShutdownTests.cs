using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Control;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
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
        var bus = A.Fake<IEventBus>();

        var handler = new EmergencyShutdownHandler(
            context,
            NullLogger<EmergencyShutdownHandler>.Instance,
            new MemoryCache(new MemoryCacheOptions()),
            controlChannel,
            bus
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
                bus.PublishAsync(
                    A<NotificationEvent>.That.Matches(e =>
                        e.Notification.Heading == "Emergency Shutdown"
                        && e.Notification.Message == "sensor failure"
                    ),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => bus.PublishAsync(A<ThermostatConfigChangedEvent>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        await using var readContext = harness.CreateContext();
        var thermostat = readContext.Thermostats.Single();
        thermostat.Mode.ShouldBe(RunMode.Disabled);
        thermostat.FanEnabled.ShouldBeFalse();
    }
}
