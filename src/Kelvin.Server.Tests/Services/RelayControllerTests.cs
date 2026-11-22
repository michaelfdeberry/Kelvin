using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Features.Gateways;
using Kelvin.Server.Messaging;
using Kelvin.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Services;

public class RelayControllerTests
{
    private readonly IDispatcher _dispatcher = A.Fake<IDispatcher>();
    private readonly List<RelayStatesChangedEvent> _published = [];

    public RelayControllerTests()
    {
        A.CallTo(() =>
                _dispatcher.PublishAsync(A<RelayStatesChangedEvent>._, A<CancellationToken>._)
            )
            .Invokes(
                (RelayStatesChangedEvent e, CancellationToken _) =>
                {
                    lock (_published)
                        _published.Add(e);
                }
            )
            .Returns(Task.CompletedTask);
    }

    // GPIO is not required, so the relays "move" in memory only, as on a development machine.
    private RelayController CreateController() =>
        new(
            NullLogger<RelayController>.Instance,
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?> { ["Gpio:Required"] = "false" }
                )
                .Build(),
            _dispatcher
        );

    // Publishing happens on a background flush, so wait for it to settle rather than assuming it has run.
    private async Task<IReadOnlyList<RelayStatesChangedEvent>> SettledAsync()
    {
        await Task.Delay(200);
        lock (_published)
            return [.. _published];
    }

    [Fact]
    public async Task PublishesTheStateOfEveryRelayWhenOneMoves()
    {
        using var controller = CreateController();

        controller.EnableHeating();

        var last = (await SettledAsync()).Last().States;
        last.Heating.ShouldBe(true);
        last.Control.ShouldBeNull();
    }

    [Fact]
    public async Task EndsOnTheFinalStateAfterABurstOfMoves()
    {
        using var controller = CreateController();

        controller.EnableControl();
        controller.EnableHeating();
        controller.EnableFan();

        var last = (await SettledAsync()).Last().States;
        last.Control.ShouldBe(true);
        last.Heating.ShouldBe(true);
        last.Fan.ShouldBe(true);
        last.Cooling.ShouldBe(false);
    }

    [Fact]
    public async Task EnableControl_PublishesOnceWithTheSettledState()
    {
        using var controller = CreateController();

        controller.EnableControl();

        var events = await SettledAsync();
        events.Count.ShouldBe(1);
        events[0]
            .States.ShouldBe(
                new GetRelayStatesResponse(
                    Heating: false,
                    Cooling: false,
                    Fan: false,
                    Control: true
                )
            );
    }

    [Fact]
    public async Task DisableControl_PublishesOnceEvenWhenSeveralRelaysWereOn()
    {
        using var controller = CreateController();
        controller.EnableControl();
        controller.EnableHeating();
        controller.EnableFan();
        await SettledAsync();
        var before = _published.Count;

        controller.DisableControl();

        var events = await SettledAsync();
        events.Count.ShouldBe(before + 1);
        events[^1]
            .States.ShouldBe(
                new GetRelayStatesResponse(
                    Heating: false,
                    Cooling: false,
                    Fan: false,
                    Control: false
                )
            );
    }

    [Fact]
    public async Task EnableDwell_PublishesOnce()
    {
        using var controller = CreateController();

        controller.EnableDwell();

        var events = await SettledAsync();
        events.Count.ShouldBe(1);
        events[0].States.Heating.ShouldBe(false);
        events[0].States.Cooling.ShouldBe(false);
    }

    [Fact]
    public async Task DoesNotPublishAMoveThatChangesNothing()
    {
        using var controller = CreateController();
        controller.EnableControl();
        var count = (await SettledAsync()).Count;

        controller.EnableControl();

        (await SettledAsync()).Count.ShouldBe(count);
    }

    [Fact]
    public async Task DoesNotPublishBeforeAnyRelayHasMoved()
    {
        using var controller = CreateController();

        (await SettledAsync()).ShouldBeEmpty();
    }
}
