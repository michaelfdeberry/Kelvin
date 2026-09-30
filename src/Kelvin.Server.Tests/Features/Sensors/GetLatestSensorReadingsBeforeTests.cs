using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Sensors;

public class GetLatestSensorReadingsBeforeTests
{
    private static async Task RecordAsync(KelvinContextHarness harness, DateTimeOffset at, params SensorPacket[] packets)
    {
        harness.Time.SetUtcNow(at);
        await using var context = harness.CreateContext();
        context.SensorPackets.AddRange(packets);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task ReturnsTheLatestPacketAtOrBeforeTheGivenTimePerSensor()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var sensor = new Sensor { Name = "Living Room", Enabled = true };
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync();

        var start = harness.Time.GetUtcNow();
        await RecordAsync(harness, start, new SensorPacket { SensorId = sensor.Id, MacAddress = "mac", TemperatureC = 20, HumidityPercentage = 40, CO2LevelPpm = 500 });
        await RecordAsync(harness, start.AddMinutes(1), new SensorPacket { SensorId = sensor.Id, MacAddress = "mac", TemperatureC = 21, HumidityPercentage = 41, CO2LevelPpm = 510 });
        await RecordAsync(harness, start.AddMinutes(5), new SensorPacket { SensorId = sensor.Id, MacAddress = "mac", TemperatureC = 99, HumidityPercentage = 99, CO2LevelPpm = 999 });

        await using var readContext = harness.CreateContext();
        var result = await new GetLatestSensorReadingsBeforeHandler(readContext).HandleAsync(
            new GetLatestSensorReadingsBeforeRequest(start.AddMinutes(2))
        );

        var reading = result.Value!.Readings.ShouldHaveSingleItem();
        reading.SensorId.ShouldBe(sensor.Id);
        reading.SensorName.ShouldBe("Living Room");
        reading.TemperatureC.ShouldBe(21);
    }

    [Fact]
    public async Task IncludesSensorsThatAreNoLongerEnabled()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var disabled = new Sensor { Name = "Removed Sensor", Enabled = false };
        context.Sensors.Add(disabled);
        await context.SaveChangesAsync();

        var start = harness.Time.GetUtcNow();
        await RecordAsync(harness, start, new SensorPacket { SensorId = disabled.Id, MacAddress = "mac", TemperatureC = 18, HumidityPercentage = 38, CO2LevelPpm = 480 });

        await using var readContext = harness.CreateContext();
        var result = await new GetLatestSensorReadingsBeforeHandler(readContext).HandleAsync(
            new GetLatestSensorReadingsBeforeRequest(start.AddMinutes(1))
        );

        var reading = result.Value!.Readings.ShouldHaveSingleItem();
        reading.SensorName.ShouldBe("Removed Sensor");
    }

    [Fact]
    public async Task ReturnsNothingWhenAllPacketsAreAfterTheGivenTime()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var sensor = new Sensor { Name = "Living Room", Enabled = true };
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync();

        var start = harness.Time.GetUtcNow();
        await RecordAsync(harness, start, new SensorPacket { SensorId = sensor.Id, MacAddress = "mac", TemperatureC = 20, HumidityPercentage = 40, CO2LevelPpm = 500 });

        await using var readContext = harness.CreateContext();
        var result = await new GetLatestSensorReadingsBeforeHandler(readContext).HandleAsync(
            new GetLatestSensorReadingsBeforeRequest(start.AddMinutes(-1))
        );

        result.Value!.Readings.ShouldBeEmpty();
    }
}
