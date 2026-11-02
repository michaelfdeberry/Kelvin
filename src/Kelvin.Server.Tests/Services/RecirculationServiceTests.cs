using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;
using static Kelvin.Server.Tests.TestHelpers.FanFixtures;

namespace Kelvin.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="Kelvin.Server.Services.RecirculationService"/> via <see cref="FanServiceHarness"/>.
/// Defaults: trigger 1000 ppm, satisfied 800 ppm, duration 60s, Average mode.
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
    public async Task AverageAboveTrigger_EnablesFanAndNotifies()
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
    public async Task PeakMode_UsesHighestAreaLevel()
    {
        var harness = FanServiceHarness.ForRecirculation();
        harness.Thermostat.CO2RecirculationMode = CO2RecirculationMode.Peak;

        await harness.StartAsync();
        await harness.PushAsync(Reading(500, Area(20, 400), Area(20, 1500)));

        harness.FanUpdates.ShouldHaveSingleItem().FanEnabled.ShouldBeTrue();

        await harness.StopAsync();
    }

    [Fact]
    public async Task AverageMode_IgnoresHighAreaWhenAverageIsLow()
    {
        var harness = FanServiceHarness.ForRecirculation();

        await harness.StartAsync();
        await harness.PushAsync(Reading(500, Area(20, 400), Area(20, 1500)));

        harness.FanUpdates.ShouldBeEmpty();

        await harness.StopAsync();
    }

    [Fact]
    public async Task PeakMode_WithNoAreas_FallsBackToAverage()
    {
        var harness = FanServiceHarness.ForRecirculation();
        harness.Thermostat.CO2RecirculationMode = CO2RecirculationMode.Peak;

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
