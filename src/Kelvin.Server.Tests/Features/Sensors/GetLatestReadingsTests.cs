using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Sensors;

public class GetLatestReadingsTests
{
    [Fact]
    public async Task ReturnsLatestPacketPerEnabledSensor()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var enabled = new Sensor { Name = "Enabled", Enabled = true };
        var disabled = new Sensor { Name = "Disabled", Enabled = false };
        context.Sensors.AddRange(enabled, disabled);
        await context.SaveChangesAsync();
        context.SensorPackets.Add(
            new SensorPacket
            {
                SensorId = enabled.Id,
                MacAddress = "enabled",
                TemperatureC = 10,
                HumidityPercentage = 20,
                CO2LevelPpm = 300,
            }
        );
        await context.SaveChangesAsync();
        harness.Time.Advance(TimeSpan.FromMinutes(1));
        context.SensorPackets.AddRange(
            new SensorPacket
            {
                SensorId = enabled.Id,
                MacAddress = "enabled",
                TemperatureC = 20,
                HumidityPercentage = 40,
                CO2LevelPpm = 500,
            },
            new SensorPacket
            {
                SensorId = disabled.Id,
                MacAddress = "disabled",
                TemperatureC = 99,
                HumidityPercentage = 99,
                CO2LevelPpm = 999,
            }
        );
        await context.SaveChangesAsync();

        var result = await new GetLatestReadingsHandler(context).HandleAsync(
            new GetLatestReadingsRequest()
        );

        result.Value!.Reading.Areas.ShouldHaveSingleItem();
        result.Value.Reading.Areas[enabled.Id].TemperatureC.ShouldBe(20);
        result.Value.Reading.TemperatureC.ShouldBe(20);
    }
}
