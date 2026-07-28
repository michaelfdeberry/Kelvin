using Kelvin.Server.Data;
using Kelvin.Server.Features.Control;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Control;

/// <summary>
/// Tests for <see cref="GetLatestControlStateChangeHandler"/>, which reads the latest recorded change for a
/// single state axis.
/// </summary>
public class GetLatestControlStateChangeTests
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
    public async Task NoHistoryForKind_ReturnsNull()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();

        var result = await new GetLatestControlStateChangeHandler(context).HandleAsync(
            new GetLatestControlStateChangeRequest(ControlChangeKind.Call)
        );

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsLatestChangeForRequestedKindOnly()
    {
        using var harness = new KelvinContextHarness();
        var start = harness.Time.GetUtcNow();

        await RecordAsync(
            harness,
            start,
            ControlStateChangeFixtures.CreateCall(ControlState.Heating)
        );
        await RecordAsync(
            harness,
            start.AddMinutes(1),
            ControlStateChangeFixtures.CreateFan(ControlState.FanOn)
        );

        await using var context = harness.CreateContext();
        var result = await new GetLatestControlStateChangeHandler(context).HandleAsync(
            new GetLatestControlStateChangeRequest(ControlChangeKind.Call)
        );

        var change = result.Value.ShouldNotBeNull();
        change.State.ShouldBe(ControlState.Heating);
        change.ChangedAt.ShouldBe(start);
    }

    [Fact]
    public async Task MultipleChangesOnSameKind_ReturnsTheMostRecentOne()
    {
        using var harness = new KelvinContextHarness();
        var start = harness.Time.GetUtcNow();

        await RecordAsync(
            harness,
            start,
            ControlStateChangeFixtures.CreateCall(ControlState.Heating)
        );
        var latest = start.AddMinutes(5);
        await RecordAsync(
            harness,
            latest,
            ControlStateChangeFixtures.CreateCall(ControlState.Dwell, ControlState.Heating)
        );

        await using var context = harness.CreateContext();
        var result = await new GetLatestControlStateChangeHandler(context).HandleAsync(
            new GetLatestControlStateChangeRequest(ControlChangeKind.Call)
        );

        var change = result.Value.ShouldNotBeNull();
        change.State.ShouldBe(ControlState.Dwell);
        change.ChangedAt.ShouldBe(latest);
    }

    [Fact]
    public async Task ExcludesSoftDeletedChanges()
    {
        using var harness = new KelvinContextHarness();
        var start = harness.Time.GetUtcNow();

        await RecordAsync(
            harness,
            start,
            ControlStateChangeFixtures.CreateCall(ControlState.Heating)
        );
        var latest = start.AddMinutes(5);
        await RecordAsync(
            harness,
            latest,
            ControlStateChangeFixtures.CreateCall(ControlState.Dwell, ControlState.Heating)
        );

        harness.Time.SetUtcNow(latest.AddMinutes(1));
        await using (var context = harness.CreateContext())
        {
            var latestChange = context.ControlStateChanges.Single(change =>
                change.State == ControlState.Dwell
            );
            context.SoftDelete(latestChange);
            await context.SaveChangesAsync();
        }

        await using var readContext = harness.CreateContext();
        var result = await new GetLatestControlStateChangeHandler(readContext).HandleAsync(
            new GetLatestControlStateChangeRequest(ControlChangeKind.Call)
        );

        var change = result.Value.ShouldNotBeNull();
        change.State.ShouldBe(ControlState.Heating);
        change.ChangedAt.ShouldBe(start);
    }
}
