using Kelvin.Server.Features.Control;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Control;

/// <summary>
/// Tests for <see cref="GetControlStatsHandler"/>, focused on the window boundaries - the run time of a call that
/// started before the window or has not finished yet is exactly what a naive aggregation gets wrong.
/// </summary>
public class GetControlStatsTests
{
    private static async Task RecordAsync(
        KelvinContextHarness harness,
        DateTimeOffset at,
        ControlStateChange change
    )
    {
        harness.Time.SetUtcNow(at);
        await using var context = harness.CreateContext();
        context.ControlStateChanges.Add(change);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task ACallThatStartedBeforeTheWindow_CountsFromTheStartOfTheWindow()
    {
        using var harness = new KelvinContextHarness();
        var start = harness.Time.GetUtcNow();

        // Heating began an hour before the window opens and stops 30 minutes into it.
        await RecordAsync(
            harness,
            start,
            ControlStateChangeFixtures.CreateCall(ControlState.Heating)
        );
        await RecordAsync(
            harness,
            start.AddMinutes(90),
            ControlStateChangeFixtures.CreateCall(ControlState.Dwell, ControlState.Heating)
        );

        var from = start.AddMinutes(60);
        var to = start.AddMinutes(120);
        harness.Time.SetUtcNow(to);

        await using var context = harness.CreateContext();
        var result = await new GetControlStatsHandler(context, harness.Time).HandleAsync(
            new GetControlStatsRequest(from, to)
        );

        var stats = result.Value.ShouldNotBeNull();
        stats.HeatingSeconds.ShouldBe(TimeSpan.FromMinutes(30).TotalSeconds);
        stats.DwellSeconds.ShouldBe(TimeSpan.FromMinutes(30).TotalSeconds);
    }

    [Fact]
    public async Task ACallThatIsStillRunning_CountsUpToTheEndOfTheWindow()
    {
        using var harness = new KelvinContextHarness();
        var start = harness.Time.GetUtcNow();

        await RecordAsync(
            harness,
            start.AddMinutes(10),
            ControlStateChangeFixtures.CreateCall(ControlState.Cooling)
        );

        var to = start.AddMinutes(40);
        harness.Time.SetUtcNow(to);

        await using var context = harness.CreateContext();
        var result = await new GetControlStatsHandler(context, harness.Time).HandleAsync(
            new GetControlStatsRequest(start, to)
        );

        var stats = result.Value.ShouldNotBeNull();
        stats.CoolingSeconds.ShouldBe(TimeSpan.FromMinutes(30).TotalSeconds);
        stats.DwellSeconds.ShouldBe(TimeSpan.FromMinutes(10).TotalSeconds);
    }

    [Fact]
    public async Task AWindowEndingInTheFuture_IsClippedToNow()
    {
        using var harness = new KelvinContextHarness();
        var start = harness.Time.GetUtcNow();

        await RecordAsync(
            harness,
            start,
            ControlStateChangeFixtures.CreateCall(ControlState.Heating)
        );

        var now = start.AddMinutes(15);
        harness.Time.SetUtcNow(now);

        await using var context = harness.CreateContext();
        // Asking for a window that runs an hour past now must not report an hour of heating that hasn't happened.
        var result = await new GetControlStatsHandler(context, harness.Time).HandleAsync(
            new GetControlStatsRequest(start, now.AddHours(1))
        );

        result
            .Value.ShouldNotBeNull()
            .HeatingSeconds.ShouldBe(TimeSpan.FromMinutes(15).TotalSeconds);
    }

    [Fact]
    public async Task CyclesCountTransitionsIntoAStateAndAverageTheirDuration()
    {
        using var harness = new KelvinContextHarness();
        var start = harness.Time.GetUtcNow();

        await RecordAsync(
            harness,
            start.AddMinutes(10),
            ControlStateChangeFixtures.CreateCall(ControlState.Heating)
        );
        await RecordAsync(
            harness,
            start.AddMinutes(30),
            ControlStateChangeFixtures.CreateCall(ControlState.Dwell, ControlState.Heating)
        );
        await RecordAsync(
            harness,
            start.AddMinutes(50),
            ControlStateChangeFixtures.CreateCall(ControlState.Heating, ControlState.Dwell)
        );
        await RecordAsync(
            harness,
            start.AddMinutes(60),
            ControlStateChangeFixtures.CreateCall(ControlState.Dwell, ControlState.Heating)
        );

        var to = start.AddMinutes(60);
        harness.Time.SetUtcNow(to);

        await using var context = harness.CreateContext();
        var result = await new GetControlStatsHandler(context, harness.Time).HandleAsync(
            new GetControlStatsRequest(start, to)
        );

        var stats = result.Value.ShouldNotBeNull();
        stats.HeatingCycles.ShouldBe(2);
        stats.HeatingSeconds.ShouldBe(TimeSpan.FromMinutes(30).TotalSeconds);
        stats.AverageHeatingCycleSeconds.ShouldBe(TimeSpan.FromMinutes(15).TotalSeconds);
        stats.CoolingCycles.ShouldBe(0);
        stats.AverageCoolingCycleSeconds.ShouldBeNull();
    }

    [Fact]
    public async Task ControlOwnershipAndFanRunTime_AreTrackedSeparatelyFromTheCall()
    {
        using var harness = new KelvinContextHarness();
        var start = harness.Time.GetUtcNow();

        await RecordAsync(
            harness,
            start.AddMinutes(10),
            ControlStateChangeFixtures.CreateControl(ControlState.Enable)
        );
        await RecordAsync(
            harness,
            start.AddMinutes(20),
            ControlStateChangeFixtures.CreateFan(ControlState.FanOn)
        );
        await RecordAsync(
            harness,
            start.AddMinutes(35),
            ControlStateChangeFixtures.CreateFan(ControlState.FanOff, ControlState.FanOn)
        );

        var to = start.AddMinutes(40);
        harness.Time.SetUtcNow(to);

        await using var context = harness.CreateContext();
        var result = await new GetControlStatsHandler(context, harness.Time).HandleAsync(
            new GetControlStatsRequest(start, to)
        );

        var stats = result.Value.ShouldNotBeNull();
        stats.RevertedSeconds.ShouldBe(TimeSpan.FromMinutes(10).TotalSeconds);
        stats.ControlledSeconds.ShouldBe(TimeSpan.FromMinutes(30).TotalSeconds);
        stats.FanSeconds.ShouldBe(TimeSpan.FromMinutes(15).TotalSeconds);
    }

    [Fact]
    public async Task AnInvertedRange_IsRejected()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var now = harness.Time.GetUtcNow();

        var result = await new GetControlStatsHandler(context, harness.Time).HandleAsync(
            new GetControlStatsRequest(now, now.AddHours(-1))
        );

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GetControlStatsErrors.InvalidRange);
    }
}
