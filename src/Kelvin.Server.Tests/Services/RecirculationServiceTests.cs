using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;
using static Kelvin.Server.Tests.TestHelpers.FanFixtures;

namespace Kelvin.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="Kelvin.Server.Services.RecirculationService"/> via <see cref="FanServiceHarness"/>.
/// Defaults: trigger 1000 ppm, satisfied 800 ppm, duration 60s.
/// </summary>
public class RecirculationServiceTests
{
    [Fact]
    public async Task Disabled_DoesNothing()
    {
        var harness = FanServiceHarness.ForRecirculation();
        harness.Thermostat.CO2RecirculationEnabled = false;
        harness.Enqueue(Reading(2000));

        await harness.StartAsync();
        await harness.AdvanceAsync(TimeSpan.FromSeconds(1));
        await harness.AdvanceAsync(TimeSpan.FromSeconds(1));

        harness.FanUpdates.ShouldBeEmpty();
        harness.Notifications.ShouldBeEmpty();

        await harness.StopAsync();
    }

    [Fact]
    public async Task DisabledMode_DoesNothing()
    {
        var harness = FanServiceHarness.ForRecirculation();
        harness.Thermostat.Mode = RunMode.Disabled;
        harness.Enqueue(Reading(2000));

        await harness.StartAsync();
        await harness.AdvanceAsync(TimeSpan.FromSeconds(1));

        harness.FanUpdates.ShouldBeEmpty();

        await harness.StopAsync();
    }

    [Fact]
    public async Task CriticalLevel_BypassesRestPeriod()
    {
        var harness = FanServiceHarness.ForRecirculation();
        var duration = TimeSpan.FromMilliseconds(harness.Thermostat.CO2RecirculationDurationMs);

        await harness.StartAsync();
        await harness.PushAsync(Reading(1200));
        await harness.AdvanceAsync(duration);

        await harness.PushAsync(Reading(1200));
        harness.FanUpdates.Select(u => u.FanEnabled).ShouldBe([true, false]);

        await harness.PushAsync(Reading(2500));
        harness.FanUpdates.Select(u => u.FanEnabled).ShouldBe([true, false, true]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task LevelDroppingBelowTrigger_ResetsTheCycleCount()
    {
        var harness = FanServiceHarness.ForRecirculation();
        var duration = TimeSpan.FromMilliseconds(harness.Thermostat.CO2RecirculationDurationMs);

        await harness.StartAsync();
        for (var cycle = 0; cycle < 9; cycle++)
        {
            await harness.PushAsync(Reading(1200));
            await harness.AdvanceAsync(duration);
            harness.Time.Advance(duration);
        }

        await harness.PushAsync(Reading(500));

        await harness.PushAsync(Reading(1200));
        await harness.AdvanceAsync(duration);
        harness.Time.Advance(duration);

        // Without the reset this would be the tenth timed out cycle and rest for 20 durations.
        await harness.PushAsync(Reading(1200));

        harness.FanUpdates.Count.ShouldBe(21);

        await harness.StopAsync();
    }

    [Fact]
    public async Task AboveTrigger_EnablesFanAndNotifies()
    {
        var harness = FanServiceHarness.ForRecirculation();

        await harness.StartAsync();
        await harness.PushAsync(Reading(1200));

        var update = harness.FanUpdates.ShouldHaveSingleItem();
        update.FanEnabled.ShouldBeTrue();
        update.Mode.ShouldBe(harness.Thermostat.Mode);

        var notification = harness.Notifications.ShouldHaveSingleItem();
        notification.Type.ShouldBe(NotificationType.Warning);
        notification.Dismissible.ShouldBeFalse();

        await harness.StopAsync();
    }

    [Theory]
    [InlineData(900)]
    [InlineData(1000)]
    public async Task AtOrBelowTrigger_DoesNothing(float co2)
    {
        var harness = FanServiceHarness.ForRecirculation();

        await harness.StartAsync();
        await harness.PushAsync(Reading(co2));

        harness.FanUpdates.ShouldBeEmpty();
        harness.Notifications.ShouldBeEmpty();

        await harness.StopAsync();
    }

    [Fact]
    public async Task HighestAreaLevel_DrivesRecirculation()
    {
        var harness = FanServiceHarness.ForRecirculation();

        await harness.StartAsync();
        await harness.PushAsync(Reading(500, Area(20, 400), Area(20, 1500)));

        harness.FanUpdates.ShouldHaveSingleItem().FanEnabled.ShouldBeTrue();

        await harness.StopAsync();
    }

    [Fact]
    public async Task NoAreas_FallsBackToTheOverallLevel()
    {
        var harness = FanServiceHarness.ForRecirculation();

        await harness.StartAsync();
        await harness.PushAsync(Reading(1200));

        harness.FanUpdates.ShouldHaveSingleItem().FanEnabled.ShouldBeTrue();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Active_LevelSatisfied_StopsFanAndReplacesNotification()
    {
        var harness = FanServiceHarness.ForRecirculation();

        await harness.StartAsync();
        await harness.PushAsync(Reading(1200));
        await harness.PushAsync(Reading(800));

        harness.FanUpdates.Select(u => u.FanEnabled).ShouldBe([true, false]);
        harness.DismissedNotificationIds.ShouldBe([harness.SentNotificationIds[0]]);
        harness.Notifications.Count.ShouldBe(2);
        harness.Notifications[1].Type.ShouldBe(NotificationType.Information);
        harness.Notifications[1].Dismissible.ShouldBeTrue();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Active_LevelBetweenThresholds_KeepsRunning()
    {
        var harness = FanServiceHarness.ForRecirculation();

        await harness.StartAsync();
        await harness.PushAsync(Reading(1200));
        await harness.PushAsync(Reading(900));

        harness.FanUpdates.Count.ShouldBe(1);
        harness.DismissedNotificationIds.ShouldBeEmpty();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Active_DurationElapses_StopsFan()
    {
        var harness = FanServiceHarness.ForRecirculation();

        await harness.StartAsync();
        await harness.PushAsync(Reading(1200));
        await harness.AdvanceAsync(
            TimeSpan.FromMilliseconds(harness.Thermostat.CO2RecirculationDurationMs)
        );

        harness.FanUpdates.Select(u => u.FanEnabled).ShouldBe([true, false]);
        harness.DismissedNotificationIds.ShouldBe([harness.SentNotificationIds[0]]);
        harness.Notifications[1].Message.ShouldContain("stopped after running for 1 minutes");

        await harness.StopAsync();
    }

    [Fact]
    public async Task AfterDurationElapses_HighLevelWithinRestPeriod_DoesNotReactivate()
    {
        var harness = FanServiceHarness.ForRecirculation();
        var duration = TimeSpan.FromMilliseconds(harness.Thermostat.CO2RecirculationDurationMs);

        await harness.StartAsync();
        await harness.PushAsync(Reading(1200));
        await harness.AdvanceAsync(duration);

        harness.Time.Advance(duration - TimeSpan.FromSeconds(1));
        await harness.PushAsync(Reading(1200));

        harness.FanUpdates.Select(u => u.FanEnabled).ShouldBe([true, false]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task AfterDurationElapses_HighLevelAfterRestPeriod_Reactivates()
    {
        var harness = FanServiceHarness.ForRecirculation();
        var duration = TimeSpan.FromMilliseconds(harness.Thermostat.CO2RecirculationDurationMs);

        await harness.StartAsync();
        await harness.PushAsync(Reading(1200));
        await harness.AdvanceAsync(duration);

        harness.Time.Advance(duration);
        await harness.PushAsync(Reading(1200));

        harness.FanUpdates.Select(u => u.FanEnabled).ShouldBe([true, false, true]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task AfterMaxCycles_RestsForTwentyDurations()
    {
        var harness = FanServiceHarness.ForRecirculation();
        var duration = TimeSpan.FromMilliseconds(harness.Thermostat.CO2RecirculationDurationMs);

        await harness.StartAsync();
        for (var cycle = 0; cycle < 10; cycle++)
        {
            await harness.PushAsync(Reading(1200));
            await harness.AdvanceAsync(duration);
            if (cycle < 9)
            {
                harness.Time.Advance(duration);
            }
        }

        harness.FanUpdates.Count.ShouldBe(20);

        harness.Time.Advance(duration * 20 - TimeSpan.FromSeconds(1));
        await harness.PushAsync(Reading(1200));
        harness.FanUpdates.Count.ShouldBe(20);

        harness.Time.Advance(TimeSpan.FromSeconds(1));
        await harness.PushAsync(Reading(1200));
        harness.FanUpdates.Count.ShouldBe(21);

        await harness.StopAsync();
    }

    [Fact]
    public async Task AfterLevelSatisfied_HighLevel_ReactivatesWithoutRestPeriod()
    {
        var harness = FanServiceHarness.ForRecirculation();

        await harness.StartAsync();
        await harness.PushAsync(Reading(1200));
        await harness.PushAsync(Reading(800));
        await harness.PushAsync(Reading(1200));

        harness.FanUpdates.Select(u => u.FanEnabled).ShouldBe([true, false, true]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task Active_CoolCallEnds_StopsFanAndStartsRestPeriod()
    {
        var harness = FanServiceHarness.ForRecirculation();

        await harness.StartAsync();
        await harness.PushAsync(Reading(1200));
        await harness.EndCoolCallAsync();

        harness.FanUpdates.Select(u => u.FanEnabled).ShouldBe([true, false]);
        harness.DismissedNotificationIds.ShouldBe([harness.SentNotificationIds[0]]);
        harness.Notifications[1].Message.ShouldContain("cooling cycle ended");

        harness.Time.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));
        await harness.PushAsync(Reading(1200));

        harness.FanUpdates.Select(u => u.FanEnabled).ShouldBe([true, false]);

        harness.Time.Advance(TimeSpan.FromSeconds(1));
        await harness.PushAsync(Reading(1200));

        harness.FanUpdates.Select(u => u.FanEnabled).ShouldBe([true, false, true]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task Inactive_CoolCallEnds_StartsRestPeriodWithoutTouchingFan()
    {
        var harness = FanServiceHarness.ForRecirculation();

        await harness.StartAsync();
        await harness.EndCoolCallAsync();

        harness.FanUpdates.ShouldBeEmpty();

        harness.Time.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));
        await harness.PushAsync(Reading(1200));

        harness.FanUpdates.ShouldBeEmpty();

        harness.Time.Advance(TimeSpan.FromSeconds(1));
        await harness.PushAsync(Reading(1200));

        harness.FanUpdates.ShouldHaveSingleItem().FanEnabled.ShouldBeTrue();

        await harness.StopAsync();
    }

    [Fact]
    public async Task Active_DisabledMidRun_StopsFanWithoutCompletionNotification()
    {
        var harness = FanServiceHarness.ForRecirculation();

        await harness.StartAsync();
        await harness.PushAsync(Reading(1200));
        harness.Thermostat.CO2RecirculationEnabled = false;
        await harness.PushAsync(Reading(1200));
        await harness.AdvanceAsync(TimeSpan.FromSeconds(1));

        harness.FanUpdates.Select(u => u.FanEnabled).ShouldBe([true, false]);
        harness.DismissedNotificationIds.ShouldBe([harness.SentNotificationIds[0]]);
        harness.Notifications.Count.ShouldBe(1);

        await harness.StopAsync();
    }

    [Fact]
    public async Task FanEnableFails_NoNotification_AndRetriesOnNextReading()
    {
        var harness = FanServiceHarness.ForRecirculation();
        harness.FailFanUpdates = true;

        await harness.StartAsync();
        harness.Enqueue(Reading(1200));
        await harness.AdvanceAsync(TimeSpan.FromSeconds(1));

        harness.Notifications.ShouldBeEmpty();

        harness.FailFanUpdates = false;
        await harness.PushAsync(Reading(1200));

        harness.FanUpdates.Count.ShouldBe(2);
        harness.Notifications.ShouldHaveSingleItem();

        await harness.StopAsync();
    }

    [Fact]
    public async Task FanStopFails_StaysActive_AndRetriesOnNextReading()
    {
        var harness = FanServiceHarness.ForRecirculation();

        await harness.StartAsync();
        await harness.PushAsync(Reading(1200));

        harness.FailFanUpdates = true;
        harness.Enqueue(Reading(800));
        await harness.AdvanceAsync(TimeSpan.FromSeconds(1));

        harness.DismissedNotificationIds.ShouldBeEmpty();

        harness.FailFanUpdates = false;
        await harness.PushAsync(Reading(800));

        harness.FanUpdates.Select(u => u.FanEnabled).ShouldBe([true, false, false]);
        harness.DismissedNotificationIds.ShouldBe([harness.SentNotificationIds[0]]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task ThermostatReadFails_RecoversAfterBackoff()
    {
        var harness = FanServiceHarness.ForRecirculation();
        harness.FailThermostatReads = true;

        await harness.StartAsync();
        harness.FailThermostatReads = false;
        await harness.AdvanceAsync(TimeSpan.FromSeconds(1));
        await harness.PushAsync(Reading(1200));

        harness.FanUpdates.ShouldHaveSingleItem().FanEnabled.ShouldBeTrue();

        await harness.StopAsync();
    }
}
