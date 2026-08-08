using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Features.Control;
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
    public async Task StartAsync_RecordsAStartupLifecycleEvent()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync(clearRecordedChanges: false);

        var change = harness.RecordedChanges.ShouldHaveSingleItem();
        change.Kind.ShouldBe(ControlChangeKind.Lifecycle);
        change.State.ShouldBe(ControlState.Startup);
        change.PreviousState.ShouldBeNull();
        change.Reason.ShouldBe("control service started");

        await harness.StopAsync();
    }

    [Fact]
    public async Task StartAsync_AfterAFault_RecordsFaultAsThePreviousStartupState()
    {
        var harness = new ControlServiceHarness();
        var previousFaultAt = harness.Time.GetUtcNow().AddMinutes(-10);
        harness.SetLatestLifecycleState(
            new GetLatestControlStateChangeResponse(ControlState.Fault, previousFaultAt)
        );

        await harness.StartAsync(clearRecordedChanges: false);

        var change = harness.RecordedChanges.ShouldHaveSingleItem();
        change.Kind.ShouldBe(ControlChangeKind.Lifecycle);
        change.State.ShouldBe(ControlState.Startup);
        change.PreviousState.ShouldBe(ControlState.Fault);
        change.PreviousStateDurationSeconds.ShouldNotBeNull();

        await harness.StopAsync();
    }

    [Fact]
    public async Task EachMessage_AppliesTheCurrentPinConfiguration()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));

        A.CallTo(() => harness.Relays.Configure(A<GetGatewayResponse>._))
            .MustHaveHappenedTwiceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Enable_TakesControlAndACallIsActuated()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));

        A.CallTo(() => harness.Relays.EnableControl()).MustHaveHappenedOnceExactly();
        A.CallTo(() => harness.Relays.EnableHeating()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task ScheduledDwell_IsAppliedWithoutAnotherMessage()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));

        harness.Time.Advance(MinimumOn - TimeSpan.FromMinutes(1));
        await harness.PushAsync(new(ControlState.Dwell));

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
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));

        harness.Time.Advance(MinimumOn - TimeSpan.FromMinutes(1));
        await harness.PushAsync(new(ControlState.Dwell));

        // The heating call is still waiting out its minimum on-time, which must not delay the fan.
        await harness.PushAsync(new(ControlState.FanOn));

        A.CallTo(() => harness.Relays.EnableFan()).MustHaveHappenedOnceExactly();
        A.CallTo(() => harness.Relays.EnableDwell()).MustNotHaveHappened();

        await harness.StopAsync();
    }

    [Fact]
    public async Task ApplyingAScheduledDwell_DoesNotDiscardAnInFlightMessage()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));

        harness.Time.Advance(MinimumOn - TimeSpan.FromMinutes(1));
        await harness.PushAsync(new(ControlState.Dwell));

        var readsBeforeTheDwellCameDue = harness.ReadCount;
        await harness.AdvanceAsync(TimeSpan.FromMinutes(1));

        // Cancelling and re-issuing the read here would drop a message the channel had already handed over.
        harness.ReadCount.ShouldBe(readsBeforeTheDwellCameDue);

        await harness.PushAsync(new(ControlState.FanOn));

        A.CallTo(() => harness.Relays.EnableFan()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task UnusableGpio_StopsTheApplication()
    {
        var harness = new ControlServiceHarness();
        A.CallTo(() => harness.Relays.Configure(A<GetGatewayResponse>._))
            .Throws(new GpioUnavailableException("The pin could not be opened."));

        await harness.StartAsync();
        harness.Deliver(new(ControlState.Enable));

        await harness.StopApplicationRequested.WaitAsync(TimeSpan.FromSeconds(5));

        A.CallTo(() => harness.Relays.EnableControl()).MustNotHaveHappened();
        harness.RecordedChanges.ShouldContain(change =>
            change.Kind == ControlChangeKind.Lifecycle && change.State == ControlState.Fault
        );

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
        await harness.PushAsync(new(requested));

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
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Disable));
        await harness.PushAsync(new(ControlState.Enable));

        A.CallTo(() => harness.Relays.EnableControl()).MustHaveHappenedTwiceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Enable_WhileAlreadyEnabled_DoesNotDisturbTheActiveCall()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));

        // ThermostatService re-asserts Enable on every cycle; doing so must not drop the heating relay or restart
        // the minimum on-time clock.
        await harness.PushAsync(new(ControlState.Enable));
        harness.Time.Advance(MinimumOn);
        await harness.PushAsync(new(ControlState.Dwell));

        A.CallTo(() => harness.Relays.EnableControl()).MustHaveHappenedOnceExactly();
        A.CallTo(() => harness.Relays.EnableDwell()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Disable_RevertsControlImmediately()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));
        await harness.PushAsync(new(ControlState.Disable));

        A.CallTo(() => harness.Relays.DisableControl()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Disable_WhileAlreadyReverted_DoesNotRestartTheMinimumOffClock()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));
        harness.Time.Advance(MinimumOn);
        await harness.PushAsync(new(ControlState.Disable));

        // ThermostatService re-asserts Disable on every cycle while the mode is Disabled.
        harness.Time.Advance(MinimumOff - TimeSpan.FromMinutes(1));
        await harness.PushAsync(new(ControlState.Disable));
        harness.Time.Advance(TimeSpan.FromMinutes(1));

        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));

        A.CallTo(() => harness.Relays.EnableHeating()).MustHaveHappenedTwiceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Heating_BeforeMinimumOffElapsed_IsBlocked()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));
        harness.Time.Advance(MinimumOn);
        await harness.PushAsync(new(ControlState.Dwell));

        await harness.PushAsync(new(ControlState.Heating));

        A.CallTo(() => harness.Relays.EnableHeating()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Heating_AfterMinimumOffElapsed_IsAllowed()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));
        harness.Time.Advance(MinimumOn);
        await harness.PushAsync(new(ControlState.Dwell));

        harness.Time.Advance(MinimumOff);
        await harness.PushAsync(new(ControlState.Heating));

        A.CallTo(() => harness.Relays.EnableHeating()).MustHaveHappenedTwiceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task ScheduledDwell_IsCancelledByAnotherCallForTheSameState()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));
        harness.Time.Advance(MinimumOn - TimeSpan.FromMinutes(1));
        await harness.PushAsync(new(ControlState.Dwell));

        await harness.PushAsync(new(ControlState.Heating));

        // The wait was cancelled, so letting the remaining minimum on-time elapse must not idle the equipment.
        harness.Time.Advance(TimeSpan.FromMinutes(1));
        await harness.PushAsync(new(ControlState.FanOn));

        A.CallTo(() => harness.Relays.EnableDwell()).MustNotHaveHappened();
        A.CallTo(() => harness.Relays.EnableHeating()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task ScheduledDwell_IsCancelledByDisable()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));
        harness.Time.Advance(MinimumOn - TimeSpan.FromMinutes(1));
        await harness.PushAsync(new(ControlState.Dwell));

        await harness.PushAsync(new(ControlState.Disable));

        harness.Time.Advance(TimeSpan.FromMinutes(1));
        await harness.PushAsync(new(ControlState.FanOn));

        A.CallTo(() => harness.Relays.DisableControl()).MustHaveHappenedOnceExactly();
        A.CallTo(() => harness.Relays.EnableDwell()).MustNotHaveHappened();

        await harness.StopAsync();
    }

    [Fact]
    public async Task RepeatedDwellRequests_TransitionOnceWhenTheWaitElapses()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));
        harness.Time.Advance(MinimumOn - TimeSpan.FromMinutes(1));
        await harness.PushAsync(new(ControlState.Dwell));
        await harness.PushAsync(new(ControlState.Dwell));

        await harness.AdvanceAsync(TimeSpan.FromMinutes(1));

        A.CallTo(() => harness.Relays.EnableDwell()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task SwitchingDirectlyFromHeatingToCooling_RevertsControl()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));
        harness.Time.Advance(MinimumOn);

        await harness.PushAsync(new(ControlState.Cooling));

        A.CallTo(() => harness.Relays.EnableCooling()).MustNotHaveHappened();
        A.CallTo(() => harness.Relays.DisableControl()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Fan_IsActuatedIndependentlyOfTheCurrentCall()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));

        await harness.PushAsync(new(ControlState.FanOn));
        await harness.PushAsync(new(ControlState.FanOff));

        A.CallTo(() => harness.Relays.EnableFan()).MustHaveHappenedOnceExactly();
        A.CallTo(() => harness.Relays.DisableFan()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Fan_DoesNotAffectTheMinimumDurationGuards()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));
        harness.Time.Advance(MinimumOn);

        await harness.PushAsync(new(ControlState.FanOn));
        await harness.PushAsync(new(ControlState.Dwell));

        A.CallTo(() => harness.Relays.EnableDwell()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Enable_RecordsAControlStateChange()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));

        var change = harness.RecordedChanges.ShouldHaveSingleItem();
        change.Kind.ShouldBe(ControlChangeKind.Control);
        change.State.ShouldBe(ControlState.Enable);
        change.PreviousState.ShouldBe(ControlState.Disable);
        change.Reason.ShouldBe("control was requested");

        await harness.StopAsync();
    }

    [Fact]
    public async Task Enable_WhileAlreadyEnabled_RecordsNothing()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        harness.RecordedChanges.Clear();

        // ThermostatService re-asserts Enable every cycle; a no-op must not fill the history with noise.
        await harness.PushAsync(new(ControlState.Enable));

        harness.RecordedChanges.ShouldBeEmpty();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Disable_RecordsTheReasonItWasReverted()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));
        harness.Time.Advance(MinimumOn);
        harness.RecordedChanges.Clear();

        // Switching straight from heating to cooling is the unsafe transition that reverts control.
        await harness.PushAsync(new(ControlState.Cooling));

        var change = harness.RecordedChanges.ShouldHaveSingleItem();
        change.Kind.ShouldBe(ControlChangeKind.Control);
        change.State.ShouldBe(ControlState.Disable);
        change.Reason.ShouldBe("an unsafe call transition was requested");

        await harness.StopAsync();
    }

    [Fact]
    public async Task ACallChange_RecordsThePreviousStateAndHowLongItRan()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));
        harness.RecordedChanges.Clear();

        harness.Time.Advance(MinimumOn);
        await harness.PushAsync(new(ControlState.Dwell));

        var change = harness.RecordedChanges.ShouldHaveSingleItem();
        change.Kind.ShouldBe(ControlChangeKind.Call);
        change.State.ShouldBe(ControlState.Dwell);
        change.PreviousState.ShouldBe(ControlState.Heating);
        change.PreviousStateDurationSeconds.ShouldBe(MinimumOn.TotalSeconds);

        await harness.StopAsync();
    }

    [Fact]
    public async Task ScheduledDwell_IsRecordedWhenItIsApplied()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));
        harness.Time.Advance(MinimumOn - TimeSpan.FromMinutes(1));
        await harness.PushAsync(new(ControlState.Dwell));
        harness.RecordedChanges.Clear();

        // Nothing has been actuated yet, so nothing may have been recorded yet either.
        harness.RecordedChanges.ShouldBeEmpty();

        await harness.AdvanceAsync(TimeSpan.FromMinutes(1));

        var change = harness.RecordedChanges.ShouldHaveSingleItem();
        change.State.ShouldBe(ControlState.Dwell);
        change.PreviousState.ShouldBe(ControlState.Heating);
        change.Reason.ShouldBe("the minimum on-time elapsed");

        await harness.StopAsync();
    }

    [Fact]
    public async Task RequestsThatActuateNothing_RecordNothing()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.Heating));
        harness.Time.Advance(MinimumOn);
        await harness.PushAsync(new(ControlState.Dwell));
        harness.RecordedChanges.Clear();

        // Blocked by the minimum off-time.
        await harness.PushAsync(new(ControlState.Heating));
        // Already dwelling.
        await harness.PushAsync(new(ControlState.Dwell));

        harness.RecordedChanges.ShouldBeEmpty();

        await harness.StopAsync();
    }

    [Fact]
    public async Task RequestsIgnoredWhileControlIsReverted_RecordNothing()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Heating));
        await harness.PushAsync(new(ControlState.FanOn));

        harness.RecordedChanges.ShouldBeEmpty();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Fan_IsRecordedOnlyWhenItActuallyChanges()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        harness.RecordedChanges.Clear();

        await harness.PushAsync(new(ControlState.FanOn));
        await harness.PushAsync(new(ControlState.FanOn));
        await harness.PushAsync(new(ControlState.FanOff));

        harness
            .RecordedChanges.Select(change => change.State)
            .ShouldBe([ControlState.FanOn, ControlState.FanOff]);
        harness.RecordedChanges.ShouldAllBe(change => change.Kind == ControlChangeKind.Fan);
        A.CallTo(() => harness.Relays.EnableFan()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }

    [Fact]
    public async Task RevertingControl_RecordsTheFanGoingOffWithIt()
    {
        var harness = new ControlServiceHarness();

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));
        await harness.PushAsync(new(ControlState.FanOn));
        harness.RecordedChanges.Clear();

        // DisableControl releases the fan relay too, so the fan timeline must not be left showing it running.
        await harness.PushAsync(new(ControlState.Disable));

        harness.RecordedChanges.Count.ShouldBe(2);
        harness.RecordedChanges[0].Kind.ShouldBe(ControlChangeKind.Control);
        harness.RecordedChanges[0].State.ShouldBe(ControlState.Disable);
        harness.RecordedChanges[1].Kind.ShouldBe(ControlChangeKind.Fan);
        harness.RecordedChanges[1].State.ShouldBe(ControlState.FanOff);
        harness.RecordedChanges[1].Reason.ShouldBe("the control relay released the fan");

        await harness.StopAsync();
    }

    [Fact]
    public async Task TheProducersContext_IsCopiedOntoTheRecordedChange()
    {
        var harness = new ControlServiceHarness();
        var scheduleId = Guid.NewGuid();
        var context = new ControlContext(
            State: ControlState.Heating,
            EnvironmentTemperatureC: 18.5f,
            HumidityPercentage: 41.0f,
            TargetTemperatureC: 21f,
            HysteresisC: 0.5f,
            ForecastTemperatureC: -3.0f,
            Mode: RunMode.Heating,
            ScheduleId: scheduleId,
            Reason: "the heating conditions were met"
        );

        await harness.StartAsync();
        await harness.PushAsync(new(State: ControlState.Enable));
        harness.RecordedChanges.Clear();

        await harness.PushAsync(context with { State = ControlState.Heating });

        var change = harness.RecordedChanges.ShouldHaveSingleItem();
        change.EnvironmentTemperatureC.ShouldBe(18.5f);
        change.HumidityPercentage.ShouldBe(41.0f);
        change.TargetTemperatureC.ShouldBe(21f);
        change.HysteresisC.ShouldBe(0.5f);
        change.ForecastTemperatureC.ShouldBe(-3.0f);
        change.Mode.ShouldBe(RunMode.Heating);
        change.ScheduleId.ShouldBe(scheduleId);
        change.Reason.ShouldBe("the heating conditions were met");

        await harness.StopAsync();
    }

    [Fact]
    public async Task AFailedRecording_DoesNotStopTheControlLoop()
    {
        var harness = new ControlServiceHarness();
        harness.SetRecordingResult(
            Result.Failure(new Error("Test.Failed", "The change could not be recorded."))
        );

        await harness.StartAsync();
        await harness.PushAsync(new(ControlState.Enable));

        // Recording is a reporting concern; losing a row must never cost control of the equipment.
        await harness.PushAsync(new(ControlState.Heating));

        A.CallTo(() => harness.Relays.EnableHeating()).MustHaveHappenedOnceExactly();

        await harness.StopAsync();
    }
}
