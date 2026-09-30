using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Sensors;

public class GetSensorHistoryPeriodsTests
{
    private static async Task RecordAsync(
        KelvinContextHarness harness,
        DateTimeOffset at,
        params SensorPacket[] packets
    )
    {
        harness.Time.SetUtcNow(at);
        await using var context = harness.CreateContext();
        context.SensorPackets.AddRange(packets);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task AveragesPacketsWithinTheSamePeriodPerSensor()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var sensor = new Sensor { Name = "Living Room", Enabled = true };
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync();

        var start = harness.Time.GetUtcNow();
        await RecordAsync(
            harness,
            start,
            new SensorPacket
            {
                SensorId = sensor.Id,
                MacAddress = "mac",
                TemperatureC = 20,
                HumidityPercentage = 40,
                CO2LevelPpm = 500,
            }
        );
        await RecordAsync(
            harness,
            start.AddSeconds(30),
            new SensorPacket
            {
                SensorId = sensor.Id,
                MacAddress = "mac",
                TemperatureC = 22,
                HumidityPercentage = 44,
                CO2LevelPpm = 520,
            }
        );

        await using var readContext = harness.CreateContext();
        var result = await new GetSensorHistoryPeriodsHandler(readContext).HandleAsync(
            new GetSensorHistoryPeriodsRequest(start, start.AddMinutes(1), PeriodSeconds: 60)
        );

        var period = result.Value!.Periods.ShouldHaveSingleItem();
        period.SensorId.ShouldBe(sensor.Id);
        period.SensorName.ShouldBe("Living Room");
        period.TemperatureC.ShouldBe(21);
        period.HumidityPercentage.ShouldBe(42);
        period.CO2LevelPpm.ShouldBe(510);
        period.SampleCount.ShouldBe(2);
    }

    [Fact]
    public async Task SeparatesPacketsThatFallInDifferentPeriods()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var sensor = new Sensor { Name = "Den", Enabled = true };
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync();

        var start = harness.Time.GetUtcNow();
        await RecordAsync(
            harness,
            start,
            new SensorPacket
            {
                SensorId = sensor.Id,
                MacAddress = "mac",
                TemperatureC = 20,
                HumidityPercentage = 40,
                CO2LevelPpm = 500,
            }
        );
        await RecordAsync(
            harness,
            start.AddSeconds(90),
            new SensorPacket
            {
                SensorId = sensor.Id,
                MacAddress = "mac",
                TemperatureC = 30,
                HumidityPercentage = 50,
                CO2LevelPpm = 600,
            }
        );

        await using var readContext = harness.CreateContext();
        var result = await new GetSensorHistoryPeriodsHandler(readContext).HandleAsync(
            new GetSensorHistoryPeriodsRequest(start, start.AddMinutes(2), PeriodSeconds: 60)
        );

        var periods = result.Value!.Periods.OrderBy(p => p.Timestamp).ToList();
        periods.Count.ShouldBe(2);
        periods[0].TemperatureC.ShouldBe(20);
        periods[1].TemperatureC.ShouldBe(30);
    }

    [Fact]
    public async Task FiltersBySensorId()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var first = new Sensor { Name = "First", Enabled = true };
        var second = new Sensor { Name = "Second", Enabled = true };
        context.Sensors.AddRange(first, second);
        await context.SaveChangesAsync();

        var start = harness.Time.GetUtcNow();
        await RecordAsync(
            harness,
            start,
            new SensorPacket
            {
                SensorId = first.Id,
                MacAddress = "first",
                TemperatureC = 10,
                HumidityPercentage = 30,
                CO2LevelPpm = 400,
            }
        );
        await RecordAsync(
            harness,
            start,
            new SensorPacket
            {
                SensorId = second.Id,
                MacAddress = "second",
                TemperatureC = 15,
                HumidityPercentage = 35,
                CO2LevelPpm = 450,
            }
        );

        await using var readContext = harness.CreateContext();
        var result = await new GetSensorHistoryPeriodsHandler(readContext).HandleAsync(
            new GetSensorHistoryPeriodsRequest(
                start,
                start.AddMinutes(1),
                first.Id,
                PeriodSeconds: 60
            )
        );

        result.Value!.Periods.ShouldHaveSingleItem().SensorId.ShouldBe(first.Id);
    }

    [Fact]
    public async Task RejectsAnInvalidRange()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var start = harness.Time.GetUtcNow();

        var result = await new GetSensorHistoryPeriodsHandler(context).HandleAsync(
            new GetSensorHistoryPeriodsRequest(start, start.AddMinutes(-1))
        );

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GetSensorHistoryPeriodsErrors.InvalidRange);
    }

    [Fact]
    public async Task ClampsAnExcessivelySmallPeriodWidthToBoundTheTotalPeriodCount()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var start = harness.Time.GetUtcNow();

        // A 30 day range with 1-second periods would be ~2.6 million iterations if left unclamped.
        var result = await new GetSensorHistoryPeriodsHandler(context).HandleAsync(
            new GetSensorHistoryPeriodsRequest(start, start.AddDays(30), PeriodSeconds: 1)
        );

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Periods.Count.ShouldBeLessThanOrEqualTo(2_000);
    }
}
