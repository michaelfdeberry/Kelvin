using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Sensors;

public class GetSensorHistoryTests
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
    public async Task ReturnsPacketsMostRecentFirstWithSensorName()
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
            start.AddMinutes(1),
            new SensorPacket
            {
                SensorId = sensor.Id,
                MacAddress = "mac",
                TemperatureC = 21,
                HumidityPercentage = 41,
                CO2LevelPpm = 510,
            }
        );

        await using var readContext = harness.CreateContext();
        var result = await new GetSensorHistoryHandler(readContext).HandleAsync(
            new GetSensorHistoryRequest()
        );

        var page = result.ShouldNotBeNull();
        page.TotalCount.ShouldBe(2);
        var items = page.Items!.ToList();
        items[0].TemperatureC.ShouldBe(21);
        items[0].SensorName.ShouldBe("Living Room");
        items[1].TemperatureC.ShouldBe(20);
    }

    [Fact]
    public async Task FiltersByRangeAndSensorId()
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
            start.AddMinutes(10),
            new SensorPacket
            {
                SensorId = second.Id,
                MacAddress = "second",
                TemperatureC = 15,
                HumidityPercentage = 35,
                CO2LevelPpm = 450,
            }
        );
        await RecordAsync(
            harness,
            start.AddMinutes(20),
            new SensorPacket
            {
                SensorId = first.Id,
                MacAddress = "first",
                TemperatureC = 12,
                HumidityPercentage = 32,
                CO2LevelPpm = 420,
            }
        );

        await using var readContext = harness.CreateContext();
        var result = await new GetSensorHistoryHandler(readContext).HandleAsync(
            new GetSensorHistoryRequest(From: start.AddMinutes(5), SensorId: first.Id)
        );

        var page = result.ShouldNotBeNull();
        page.Items!.ShouldHaveSingleItem().TemperatureC.ShouldBe(12);
    }

    [Fact]
    public async Task ClampsAnOversizedPageSize()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();

        var result = await new GetSensorHistoryHandler(context).HandleAsync(
            new GetSensorHistoryRequest(PageSize: 100_000)
        );

        result.ShouldNotBeNull().PageSize.ShouldBe(Kelvin.Server.Application.Paging.MaxPageSize);
    }

    [Fact]
    public async Task RejectsAnInvalidRange()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var start = harness.Time.GetUtcNow();

        var result = await new GetSensorHistoryHandler(context).HandleAsync(
            new GetSensorHistoryRequest(From: start, To: start.AddMinutes(-1))
        );

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GetSensorHistoryErrors.InvalidRange);
    }
}
