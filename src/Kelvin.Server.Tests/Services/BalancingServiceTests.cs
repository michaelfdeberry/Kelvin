using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;
using static Kelvin.Server.Tests.TestHelpers.FanFixtures;

namespace Kelvin.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="Kelvin.Server.Services.BalancingService"/> via <see cref="FanServiceHarness"/>.
/// Defaults: trigger 3.0C, satisfied 1.5C, duration 60s, Heating mode.
/// </summary>
public class BalancingServiceTests
{
    /// <summary>
    /// Starts the service with the given reading as the latest one, then ends a heat call. The cycle is evaluated
    /// against that reading as soon as the call ends, without waiting for another one.
    /// </summary>
    private static async Task StartAfterHeatCycleAsync(
        FanServiceHarness harness,
        params float[] areaTemperaturesC
    )
    {
        await harness.StartAsync();
        await harness.PushAsync(Spread(areaTemperaturesC));
        await harness.EndHeatCallAsync();
    }

    [Theory]
    [InlineData(RunMode.Heating)]
    [InlineData(RunMode.Automatic)]
    public async Task SpreadAboveTrigger_AfterHeatCycle_EnablesFanAndNotifies(RunMode mode)
    {
        var harness = FanServiceHarness.ForBalancing();
        harness.Thermostat.Mode = mode;

        await StartAfterHeatCycleAsync(harness, 20, 24);

        var update = harness.FanUpdates.ShouldHaveSingleItem();
        update.FanEnabled.ShouldBeTrue();
        update.Mode.ShouldBe(mode);

        var notification = harness.Notifications.ShouldHaveSingleItem();
        notification.Type.ShouldBe(NotificationType.Information);
        notification.Dismissible.ShouldBeFalse();

        await harness.StopAsync();
    }

    [Fact]
    public async Task HeatCycleEndingBeforeFirstReading_IsEvaluatedWhenOneArrives()
    {
        var harness = FanServiceHarness.ForBalancing();

        await harness.StartAsync();
        await harness.EndHeatCallAsync();
        harness.FanUpdates.ShouldBeEmpty();

        await harness.PushAsync(Spread(20, 24));

        harness.FanUpdates.ShouldHaveSingleItem().FanEnabled.ShouldBeTrue();

        await harness.StopAsync();
    }

    [Fact]
    public async Task SpreadAtTrigger_AfterHeatCycle_DoesNothing()
    {
        var harness = FanServiceHarness.ForBalancing();

        await StartAfterHeatCycleAsync(harness, 20, 23);

        harness.FanUpdates.ShouldBeEmpty();

        await harness.StopAsync();
    }

    [Fact]
    public async Task LargeSpread_WithoutHeatCycle_DoesNothing()
    {
        var harness = FanServiceHarness.ForBalancing();

        await harness.StartAsync();
        await harness.PushAsync(Spread(20, 26));
        await harness.PushAsync(Spread(20, 26));

        harness.FanUpdates.ShouldBeEmpty();

        await harness.StopAsync();
    }

    [Fact]
    public async Task CoolingCycleCompleting_IsNotAHeatCycle()
    {
        var harness = FanServiceHarness.ForBalancing();

        await harness.StartAsync();
        await harness.PushAsync(Spread(20, 26));
        harness.RaiseCallChange(ControlState.Cooling, ControlState.Dwell);
        await harness.PushAsync(Spread(20, 26));

        harness.FanUpdates.ShouldBeEmpty();

        await harness.StopAsync();
    }

    [Fact]
    public async Task HeatCycleIsEvaluatedOnce()
    {
        var harness = FanServiceHarness.ForBalancing();

        await StartAfterHeatCycleAsync(harness, 20, 22);
        await harness.PushAsync(Spread(20, 26));

        harness.FanUpdates.ShouldBeEmpty();

        await harness.StopAsync();
    }

    [Theory]
    [InlineData(RunMode.Cooling)]
    [InlineData(RunMode.Off)]
    [InlineData(RunMode.Disabled)]
    public async Task IneligibleMode_DoesNothing(RunMode mode)
    {
        var harness = FanServiceHarness.ForBalancing();
        harness.Thermostat.Mode = mode;
        harness.Enqueue(Spread(20, 26));

        await harness.StartAsync();
        harness.RaiseCallChange(ControlState.Heating, ControlState.Dwell);
        await harness.AdvanceAsync(TimeSpan.FromSeconds(1));
        await harness.AdvanceAsync(TimeSpan.FromSeconds(1));

        harness.FanUpdates.ShouldBeEmpty();

        await harness.StopAsync();
    }

    [Fact]
    public async Task AutoBalanceDisabled_DoesNothing()
    {
        var harness = FanServiceHarness.ForBalancing();
        harness.Thermostat.HeatingAutoBalanceEnabled = false;
        harness.Enqueue(Spread(20, 26));

        await harness.StartAsync();
        harness.RaiseCallChange(ControlState.Heating, ControlState.Dwell);
        await harness.AdvanceAsync(TimeSpan.FromSeconds(1));
        await harness.AdvanceAsync(TimeSpan.FromSeconds(1));

        harness.FanUpdates.ShouldBeEmpty();

        await harness.StopAsync();
    }

    [Fact]
    public async Task FanAlreadyOn_DoesNotStartBalancing()
    {
        var harness = FanServiceHarness.ForBalancing();
        harness.Thermostat.FanEnabled = true;

        await StartAfterHeatCycleAsync(harness, 20, 26);

        harness.FanUpdates.ShouldBeEmpty();

        await harness.StopAsync();
    }

    [Fact]
    public async Task FewerThanTwoAreas_DoesNothing()
    {
        var harness = FanServiceHarness.ForBalancing();

        await StartAfterHeatCycleAsync(harness, 20);

        harness.FanUpdates.ShouldBeEmpty();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Active_SpreadSatisfied_StopsFanAndReplacesNotification()
    {
        var harness = FanServiceHarness.ForBalancing();

        await StartAfterHeatCycleAsync(harness, 20, 24);
        await harness.PushAsync(Spread(20, 21.5f));

        harness.FanUpdates.Select(u => u.FanEnabled).ShouldBe([true, false]);
        harness.DismissedNotificationIds.ShouldBe([harness.SentNotificationIds[0]]);
        harness.Notifications.Count.ShouldBe(2);
        harness.Notifications[1].Dismissible.ShouldBeTrue();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Active_SpreadStillHigh_KeepsRunning()
    {
        var harness = FanServiceHarness.ForBalancing();

        await StartAfterHeatCycleAsync(harness, 20, 24);
        await harness.PushAsync(Spread(20, 22));

        harness.FanUpdates.Count.ShouldBe(1);
        harness.DismissedNotificationIds.ShouldBeEmpty();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Active_DurationElapses_StopsFan()
    {
        var harness = FanServiceHarness.ForBalancing();

        await StartAfterHeatCycleAsync(harness, 20, 24);
        await harness.AdvanceAsync(
            TimeSpan.FromMilliseconds(harness.Thermostat.HeatingAutoBalanceDurationMs)
        );

        harness.FanUpdates.Select(u => u.FanEnabled).ShouldBe([true, false]);
        harness.DismissedNotificationIds.ShouldBe([harness.SentNotificationIds[0]]);
        harness.Notifications[1].Message.ShouldContain("stopped after running for 1 minutes");

        await harness.StopAsync();
    }

    [Fact]
    public async Task Active_DisabledMidRun_StopsFanWithoutCompletionNotification()
    {
        var harness = FanServiceHarness.ForBalancing();

        await StartAfterHeatCycleAsync(harness, 20, 24);
        harness.Thermostat.HeatingAutoBalanceEnabled = false;
        await harness.PushAsync(Spread(20, 24));
        await harness.AdvanceAsync(TimeSpan.FromSeconds(1));

        harness.FanUpdates.Select(u => u.FanEnabled).ShouldBe([true, false]);
        harness.DismissedNotificationIds.ShouldBe([harness.SentNotificationIds[0]]);
        harness.Notifications.Count.ShouldBe(1);

        await harness.StopAsync();
    }

    [Fact]
    public async Task FanEnableFails_RetriesOnNextReading()
    {
        var harness = FanServiceHarness.ForBalancing();

        await harness.StartAsync();
        await harness.PushAsync(Spread(20, 24));

        harness.FailFanUpdates = true;
        harness.RaiseCallChange(ControlState.Heating, ControlState.Dwell);
        await harness.AdvanceAsync(TimeSpan.FromSeconds(1));

        harness.Notifications.ShouldBeEmpty();

        harness.FailFanUpdates = false;
        await harness.PushAsync(Spread(20, 24));

        harness.FanUpdates.Count.ShouldBe(2);
        harness.Notifications.ShouldHaveSingleItem();

        await harness.StopAsync();
    }

    [Fact]
    public async Task FanStopFails_StaysActive_AndRetriesOnNextReading()
    {
        var harness = FanServiceHarness.ForBalancing();

        await StartAfterHeatCycleAsync(harness, 20, 24);

        harness.FailFanUpdates = true;
        harness.Enqueue(Spread(20, 20));
        await harness.AdvanceAsync(TimeSpan.FromSeconds(1));

        harness.DismissedNotificationIds.ShouldBeEmpty();

        harness.FailFanUpdates = false;
        await harness.PushAsync(Spread(20, 20));

        harness.FanUpdates.Select(u => u.FanEnabled).ShouldBe([true, false, false]);
        harness.DismissedNotificationIds.ShouldBe([harness.SentNotificationIds[0]]);

        await harness.StopAsync();
    }
}
