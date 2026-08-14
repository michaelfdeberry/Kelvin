using FakeItEasy;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Sensors;

/// <summary>
/// Tests for <see cref="SaveSensorPacketHandler"/>, which links a packet to a sensor (creating one if needed)
/// before persisting it and publishing it to <see cref="ISensorPacketChannel"/>.
/// </summary>
public class SaveSensorPacketTests
{
    private static SensorPacket CreatePacket(string macAddress, float? batteryLevelPercentage = 90f) =>
        new()
        {
            MacAddress = macAddress,
            TemperatureC = 21.5f,
            HumidityPercentage = 45f,
            CO2LevelPpm = 600,
            BatteryLevelPercentage = batteryLevelPercentage,
        };

    [Fact]
    public async Task NewSensor_CreatesSensorAndLinksPacket()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var channel = A.Fake<ISensorPacketChannel>();

        var packet = CreatePacket("aa:bb:cc:dd:ee:ff");
        var result = await new SaveSensorPacketHandler(context, channel).HandleAsync(
            new SaveSensorPacketRequest(packet)
        );

        result.IsSuccess.ShouldBeTrue();

        await using var readContext = harness.CreateContext();
        var sensor = readContext.Sensors.Single();
        sensor.MacAddress.ShouldBe("aa:bb:cc:dd:ee:ff");

        var savedPacket = readContext.SensorPackets.Single();
        savedPacket.SensorId.ShouldBe(sensor.Id);
    }

    [Fact]
    public async Task ExistingSensor_ReusesSensorRecord()
    {
        using var harness = new KelvinContextHarness();
        Guid existingSensorId;
        await using (var context = harness.CreateContext())
        {
            var sensor = new Sensor { MacAddress = "aa:bb:cc:dd:ee:ff" };
            context.Sensors.Add(sensor);
            await context.SaveChangesAsync();
            existingSensorId = sensor.Id;
        }

        await using var writeContext = harness.CreateContext();
        var channel = A.Fake<ISensorPacketChannel>();
        var packet = CreatePacket("aa:bb:cc:dd:ee:ff");
        var result = await new SaveSensorPacketHandler(writeContext, channel).HandleAsync(
            new SaveSensorPacketRequest(packet)
        );

        result.IsSuccess.ShouldBeTrue();

        await using var readContext = harness.CreateContext();
        readContext.Sensors.Count().ShouldBe(1);
        readContext.SensorPackets.Single().SensorId.ShouldBe(existingSensorId);
    }

    [Fact]
    public async Task PublishesSavedPacketToChannel()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var channel = A.Fake<ISensorPacketChannel>();

        var packet = CreatePacket("aa:bb:cc:dd:ee:ff");
        await new SaveSensorPacketHandler(context, channel).HandleAsync(
            new SaveSensorPacketRequest(packet)
        );

        A.CallTo(() => channel.WriteAsync(packet, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task NullBattery_PersistsAndPublishesPacket()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var channel = A.Fake<ISensorPacketChannel>();

        var packet = CreatePacket("aa:bb:cc:dd:ee:ff", null);
        var result = await new SaveSensorPacketHandler(context, channel).HandleAsync(
            new SaveSensorPacketRequest(packet)
        );

        result.IsSuccess.ShouldBeTrue();

        await using var readContext = harness.CreateContext();
        var savedPacket = readContext.SensorPackets.Single();
        savedPacket.BatteryLevelPercentage.ShouldBeNull();

        A.CallTo(() => channel.WriteAsync(packet, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
