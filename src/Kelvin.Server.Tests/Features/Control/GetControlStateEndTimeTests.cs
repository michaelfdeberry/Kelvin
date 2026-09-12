using Kelvin.Server.Features.Control;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Control;

public class GetControlStateEndTimeTests
{
    [Fact]
    public async Task ReturnsLatestMatchingDwellChange()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        context.ControlStateChanges.Add(new ControlStateChange
        {
            Kind = ControlChangeKind.Call,
            State = ControlState.Dwell,
            PreviousState = ControlState.Heating,
        });
        await context.SaveChangesAsync();

        var result = await new GetControlStateEndTimeHandler(context).HandleAsync(
            new GetControlStateEndTimeRequest(ControlState.Heating)
        );

        result.IsSuccess.ShouldBeTrue();
        result.Value!.ChangedAt.ShouldNotBeNull();
    }
}