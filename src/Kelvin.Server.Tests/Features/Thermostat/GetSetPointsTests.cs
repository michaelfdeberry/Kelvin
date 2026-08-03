using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Thermostat;

/// <summary>
/// Tests for <see cref="GetSetPointsHandler"/>.
/// </summary>
public class GetSetPointsTests
{
    [Fact]
    public async Task NoSetPoints_ReturnsEmptyCollection()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();

        var result = await new GetSetPointsHandler(context).HandleAsync(new GetSetPointsRequest());

        result.Value.ShouldNotBeNull().SetPoints.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReturnsAllSetPointsMappedToDto()
    {
        using var harness = new KelvinContextHarness();
        Guid setPointId;
        await using (var context = harness.CreateContext())
        {
            var thermostat = new Models.Thermostat { Mode = RunMode.Cooling, FanEnabled = false };
            var setPoint = new SetPoint { Type = RunType.Cooling, TargetTemperatureC = 24f };
            thermostat.SetPoints.Add(setPoint);
            context.Thermostats.Add(thermostat);
            await context.SaveChangesAsync();
            setPointId = setPoint.Id;
        }

        await using var readContext = harness.CreateContext();
        var result = await new GetSetPointsHandler(readContext).HandleAsync(
            new GetSetPointsRequest()
        );

        var response = result.Value.ShouldNotBeNull();
        var dto = response.SetPoints.ShouldHaveSingleItem();
        dto.Id.ShouldBe(setPointId);
        dto.Type.ShouldBe(RunType.Cooling);
        dto.TargetTemperatureC.ShouldBe(24f);
    }
}
