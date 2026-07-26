using FakeItEasy;
using Kelvin.Server.Features.Gateways;
using Kelvin.Server.Models;
using Kelvin.Server.Services;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="ControlService"/>, exercised entirely through its public
/// <see cref="Microsoft.Extensions.Hosting.BackgroundService"/> surface via <see cref="ControlServiceHarness"/>.
/// All constructor dependencies (IControlChannel, IDispatcher, IRelayController, IHostApplicationLifetime) are
/// FakeItEasy fakes and the clock is a FakeTimeProvider, so the minimum on/off duration guards are driven without
/// wall-clock waiting.
/// </summary>
public class ControlServiceTests
{
    private static readonly TimeSpan MinimumOn = TimeSpan.FromMinutes(
        ControlFixtures.MinimumOnMinutes
    );

    private static readonly TimeSpan MinimumOff = TimeSpan.FromMinutes(
        ControlFixtures.MinimumOffMinutes
    );

    [Fact]
    public async Task StartAsync_InitializesTheRelays()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();

        A.CallTo(() => harness.Relays.Initialize()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task EachMessage_AppliesTheCurrentPinConfiguration()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(ControlState.Enable);
        await harness.PushAsync(ControlState.Heating);

        A.CallTo(() => harness.Relays.Configure(A<GetGatewayResponse>._))
            .MustHaveHappenedTwiceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Enable_TakesControlAndACallIsActuated()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(ControlState.Enable);
        await harness.PushAsync(ControlState.Heating);

        A.CallTo(() => harness.Relays.EnableControl()).MustHaveHappenedOnceExactly();
        A.CallTo(() => harness.Relays.EnableHeating()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task ScheduledDwell_IsAppliedWithoutAnotherMessage()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(ControlState.Enable);
        await harness.PushAsync(ControlState.Heating);

        harness.Time.Advance(MinimumOn - TimeSpan.FromMinutes(1));
        await harness.PushAsync(ControlState.Dwell);

        A.CallTo(() => harness.Relays.EnableDwell()).MustNotHaveHappened();

        await harness.AdvanceAsync(TimeSpan.FromMinutes(1));

        A.CallTo(() => harness.Relays.EnableDwell()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task MessagesUnrelatedToTheCall_AreNotHeldUpByAPendingDwell()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(ControlState.Enable);
        await harness.PushAsync(ControlState.Heating);

        harness.Time.Advance(MinimumOn - TimeSpan.FromMinutes(1));
        await harness.PushAsync(ControlState.Dwell);

        // The heating call is still waiting out its minimum on-time, which must not delay the fan.
        await harness.PushAsync(ControlState.FanOff);

        A.CallTo(() => harness.Relays.DisableFan()).MustHaveHappenedOnceExactly();
        A.CallTo(() => harness.Relays.EnableDwell()).MustNotHaveHappened();

        await harness.StopAsync();
    }

    [Fact]
    public async Task ApplyingAScheduledDwell_DoesNotDiscardAnInFlightMessage()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(ControlState.Enable);
        await harness.PushAsync(ControlState.Heating);

        harness.Time.Advance(MinimumOn - TimeSpan.FromMinutes(1));
        await harness.PushAsync(ControlState.Dwell);

        var readsBeforeTheDwellCameDue = harness.ReadCount;
        await harness.AdvanceAsync(TimeSpan.FromMinutes(1));

        // Cancelling and re-issuing the read here would drop a message the channel had already handed over.
        harness.ReadCount.ShouldBe(readsBeforeTheDwellCameDue);

        await harness.PushAsync(ControlState.FanOff);

        A.CallTo(() => harness.Relays.DisableFan()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task UnusableGpio_StopsTheApplication()
    {
        var harness = new ControlServiceHarness();
        A.CallTo(() => harness.Relays.Configure(A<GetGatewayResponse>._))
            .Throws(new GpioUnavailableException("The pin could not be opened."));

        await harness.StartAsync();
        harness.Deliver(ControlState.Enable);

        await harness.StopApplicationRequested.WaitAsync(TimeSpan.FromSeconds(5));

        A.CallTo(() => harness.Relays.EnableControl()).MustNotHaveHappened();

        await harness.StopAsync();
    }

    [Theory]
    [InlineData(ControlState.Heating)]
    [InlineData(ControlState.Cooling)]
    [InlineData(ControlState.Dwell)]
    [InlineData(ControlState.FanOn)]
    [InlineData(ControlState.FanOff)]
    public async Task RequestsAreIgnoredWhileControlIsReverted(ControlState requested)
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(requested);

        A.CallTo(() => harness.Relays.EnableControl()).MustNotHaveHappened();
        A.CallTo(() => harness.Relays.EnableHeating()).MustNotHaveHappened();
        A.CallTo(() => harness.Relays.EnableCooling()).MustNotHaveHappened();
        A.CallTo(() => harness.Relays.EnableDwell()).MustNotHaveHappened();
        A.CallTo(() => harness.Relays.EnableFan()).MustNotHaveHappened();
        A.CallTo(() => harness.Relays.DisableFan()).MustNotHaveHappened();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Enable_AfterDisable_RetakesControl()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(ControlState.Enable);
        await harness.PushAsync(ControlState.Disable);
        await harness.PushAsync(ControlState.Enable);

        A.CallTo(() => harness.Relays.EnableControl()).MustHaveHappenedTwiceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Enable_WhileAlreadyEnabled_DoesNotDisturbTheActiveCall()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(ControlState.Enable);
        await harness.PushAsync(ControlState.Heating);

        // ThermostatService re-asserts Enable on every cycle; doing so must not drop the heating relay or restart
        // the minimum on-time clock.
        await harness.PushAsync(ControlState.Enable);
        harness.Time.Advance(MinimumOn);
        await harness.PushAsync(ControlState.Dwell);

        A.CallTo(() => harness.Relays.EnableControl()).MustHaveHappenedOnceExactly();
        A.CallTo(() => harness.Relays.EnableDwell()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Disable_RevertsControlImmediately()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(ControlState.Enable);
        await harness.PushAsync(ControlState.Heating);
        await harness.PushAsync(ControlState.Disable);

        A.CallTo(() => harness.Relays.DisableControl()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Disable_WhileAlreadyReverted_DoesNotRestartTheMinimumOffClock()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(ControlState.Enable);
        await harness.PushAsync(ControlState.Heating);
        harness.Time.Advance(MinimumOn);
        await harness.PushAsync(ControlState.Disable);

        // ThermostatService re-asserts Disable on every cycle while the mode is Disabled.
        harness.Time.Advance(MinimumOff - TimeSpan.FromMinutes(1));
        await harness.PushAsync(ControlState.Disable);
        harness.Time.Advance(TimeSpan.FromMinutes(1));

        await harness.PushAsync(ControlState.Enable);
        await harness.PushAsync(ControlState.Heating);

        A.CallTo(() => harness.Relays.EnableHeating()).MustHaveHappenedTwiceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Heating_BeforeMinimumOffElapsed_IsBlocked()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(ControlState.Enable);
        await harness.PushAsync(ControlState.Heating);
        harness.Time.Advance(MinimumOn);
        await harness.PushAsync(ControlState.Dwell);

        await harness.PushAsync(ControlState.Heating);

        A.CallTo(() => harness.Relays.EnableHeating()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Heating_AfterMinimumOffElapsed_IsAllowed()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(ControlState.Enable);
        await harness.PushAsync(ControlState.Heating);
        harness.Time.Advance(MinimumOn);
        await harness.PushAsync(ControlState.Dwell);

        harness.Time.Advance(MinimumOff);
        await harness.PushAsync(ControlState.Heating);

        A.CallTo(() => harness.Relays.EnableHeating()).MustHaveHappenedTwiceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task ScheduledDwell_IsCancelledByAnotherCallForTheSameState()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(ControlState.Enable);
        await harness.PushAsync(ControlState.Heating);
        harness.Time.Advance(MinimumOn - TimeSpan.FromMinutes(1));
        await harness.PushAsync(ControlState.Dwell);

        await harness.PushAsync(ControlState.Heating);

        // The wait was cancelled, so letting the remaining minimum on-time elapse must not idle the equipment.
        harness.Time.Advance(TimeSpan.FromMinutes(1));
        await harness.PushAsync(ControlState.FanOn);

        A.CallTo(() => harness.Relays.EnableDwell()).MustNotHaveHappened();
        A.CallTo(() => harness.Relays.EnableHeating()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task ScheduledDwell_IsCancelledByDisable()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(ControlState.Enable);
        await harness.PushAsync(ControlState.Heating);
        harness.Time.Advance(MinimumOn - TimeSpan.FromMinutes(1));
        await harness.PushAsync(ControlState.Dwell);

        await harness.PushAsync(ControlState.Disable);

        harness.Time.Advance(TimeSpan.FromMinutes(1));
        await harness.PushAsync(ControlState.FanOn);

        A.CallTo(() => harness.Relays.DisableControl()).MustHaveHappenedOnceExactly();
        A.CallTo(() => harness.Relays.EnableDwell()).MustNotHaveHappened();

        await harness.StopAsync();
    }

    [Fact]
    public async Task RepeatedDwellRequests_TransitionOnceWhenTheWaitElapses()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(ControlState.Enable);
        await harness.PushAsync(ControlState.Heating);
        harness.Time.Advance(MinimumOn - TimeSpan.FromMinutes(1));
        await harness.PushAsync(ControlState.Dwell);
        await harness.PushAsync(ControlState.Dwell);

        await harness.AdvanceAsync(TimeSpan.FromMinutes(1));

        A.CallTo(() => harness.Relays.EnableDwell()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task SwitchingDirectlyFromHeatingToCooling_RevertsControl()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(ControlState.Enable);
        await harness.PushAsync(ControlState.Heating);
        harness.Time.Advance(MinimumOn);

        await harness.PushAsync(ControlState.Cooling);

        A.CallTo(() => harness.Relays.EnableCooling()).MustNotHaveHappened();
        A.CallTo(() => harness.Relays.DisableControl()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Fan_IsActuatedIndependentlyOfTheCurrentCall()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(ControlState.Enable);
        await harness.PushAsync(ControlState.Heating);

        await harness.PushAsync(ControlState.FanOn);
        await harness.PushAsync(ControlState.FanOff);

        A.CallTo(() => harness.Relays.EnableFan()).MustHaveHappenedOnceExactly();
        A.CallTo(() => harness.Relays.DisableFan()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Fan_DoesNotAffectTheMinimumDurationGuards()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(ControlState.Enable);
        await harness.PushAsync(ControlState.Heating);
        harness.Time.Advance(MinimumOn);

        await harness.PushAsync(ControlState.FanOn);
        await harness.PushAsync(ControlState.Dwell);

        A.CallTo(() => harness.Relays.EnableDwell()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }
}
