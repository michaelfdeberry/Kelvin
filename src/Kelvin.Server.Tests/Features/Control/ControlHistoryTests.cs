using Kelvin.Server.Features.Control;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Control;

/// <summary>
/// Tests for the control history read handlers, exercised against a real
/// <see cref="Kelvin.Server.Data.KelvinContext"/> over in-memory SQLite so the queries are really translated.
/// </summary>
/// <remarks>
/// History rows are positioned on the timeline by advancing the harness clock before saving, because
/// <see cref="Kelvin.Server.Models.Entity.CreatedAt"/> is stamped by the interceptor and cannot be assigned.
/// </remarks>
public class ControlHistoryTests
{
    private static async Task RecordAsync(
        KelvinContextHarness harness,
        DateTimeOffset at,
        params ControlStateChange[] changes
    )
    {
        harness.Time.SetUtcNow(at);
        await using var context = harness.CreateContext();
        context.ControlStateChanges.AddRange(changes);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetControlState_WithNoHistory_ReportsTheFailsafeState()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();

        var result = await new GetControlStateHandler(context).HandleAsync(
            new GetControlStateRequest()
        );

        var state = result.Value.ShouldNotBeNull();
        state.ControlState.ShouldBe(ControlState.Disable);
        state.CallState.ShouldBe(ControlState.Dwell);
        state.FanOn.ShouldBeFalse();
        state.LastChange.ShouldBeNull();
    }

    [Fact]
    public async Task GetControlState_ReportsTheLatestChangeOnEachAxisIndependently()
    {
        using var harness = new KelvinContextHarness();
        var start = harness.Time.GetUtcNow();

        await RecordAsync(
            harness,
            start,
            ControlStateChangeFixtures.CreateControl(ControlState.Enable)
        );
        await RecordAsync(
            harness,
            start.AddMinutes(1),
            ControlStateChangeFixtures.CreateFan(ControlState.FanOn)
        );
        await RecordAsync(
            harness,
            start.AddMinutes(2),
            ControlStateChangeFixtures.CreateCall(ControlState.Heating)
        );
        var latest = start.AddMinutes(3);
        await RecordAsync(
            harness,
            latest,
            ControlStateChangeFixtures.CreateCall(ControlState.Dwell, ControlState.Heating)
        );

        await using var context = harness.CreateContext();
        var result = await new GetControlStateHandler(context).HandleAsync(
            new GetControlStateRequest()
        );

        var state = result.Value.ShouldNotBeNull();
        // The fan and control axes are untouched by the call moving on.
        state.ControlState.ShouldBe(ControlState.Enable);
        state.ControlSince.ShouldBe(start);
        state.FanOn.ShouldBeTrue();
        state.CallState.ShouldBe(ControlState.Dwell);
        state.CallSince.ShouldBe(latest);
        state.LastChange.ShouldNotBeNull().State.ShouldBe(ControlState.Dwell);
    }

    [Fact]
    public async Task GetControlHistory_ReturnsTheMostRecentChangesFirst()
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
            ControlStateChangeFixtures.CreateCall(ControlState.Dwell)
        );

        await using var context = harness.CreateContext();
        var result = await new GetControlHistoryHandler(context).HandleAsync(
            new GetControlHistoryRequest()
        );

        var page = result.ShouldNotBeNull();
        page.TotalCount.ShouldBe(2);
        page.Items?.Select(item => item.State).ShouldBe([ControlState.Dwell, ControlState.Heating]);
    }

    [Fact]
    public async Task GetControlHistory_FiltersByKindAndRange()
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
            start.AddMinutes(10),
            ControlStateChangeFixtures.CreateFan(ControlState.FanOn)
        );
        await RecordAsync(
            harness,
            start.AddMinutes(20),
            ControlStateChangeFixtures.CreateCall(ControlState.Dwell)
        );

        await using var context = harness.CreateContext();
        var result = await new GetControlHistoryHandler(context).HandleAsync(
            new GetControlHistoryRequest(From: start.AddMinutes(5), Kind: ControlChangeKind.Call)
        );

        var page = result.ShouldNotBeNull();
        page.Items?.ShouldHaveSingleItem().State.ShouldBe(ControlState.Dwell);
    }

    [Fact]
    public async Task GetControlHistory_ClampsAnOversizedPageSize()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();

        // A caller must not be able to ask for the whole table in one request.
        var result = await new GetControlHistoryHandler(context).HandleAsync(
            new GetControlHistoryRequest(PageSize: 100_000)
        );

        result.ShouldNotBeNull().PageSize.ShouldBe(Application.Paging.MaxPageSize);
    }

    [Fact]
    public async Task GetControlHistory_RejectsAnInvertedRange()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var now = harness.Time.GetUtcNow();

        var result = await new GetControlHistoryHandler(context).HandleAsync(
            new GetControlHistoryRequest(From: now, To: now.AddHours(-1))
        );

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GetControlHistoryErrors.InvalidRange);
    }
}
