using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Sensors;

/// <summary>
/// Tests for <see cref="SaveSensorPacketHandler"/>, which links a packet to a sensor (creating one if needed)
/// before persisting it and publishing it to <see cref="ISensorPacketChannel"/>.
/// </summary>
public class SaveSensorPacketTests
{
    private static SensorPacket CreatePacket(
        string macAddress,
        float? batteryLevelPercentage = 90f
    ) =>
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
        var cache = A.Fake<IMemoryCache>();

        var dispatcher = A.Fake<IDispatcher>();

        var packet = CreatePacket("aa:bb:cc:dd:ee:ff");
        var result = await new SaveSensorPacketHandler(
            context,
            channel,
            cache,
            dispatcher
        ).HandleAsync(new SaveSensorPacketRequest(packet, DeviceType.Node));

        result.IsSuccess.ShouldBeTrue();

        await using var readContext = harness.CreateContext();
        var sensor = readContext.Sensors.Single();
        sensor.MacAddress.ShouldBe("aa:bb:cc:dd:ee:ff");

        var savedPacket = readContext.SensorPackets.Single();
        savedPacket.SensorId.ShouldBe(sensor.Id);

        A.CallTo(() =>
                dispatcher.PublishAsync(
                    A<SensorChangedEvent>.That.Matches(e => e.Sensor.Id == sensor.Id),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
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
        var cache = A.Fake<IMemoryCache>();
        var dispatcher = A.Fake<IDispatcher>();
        var packet = CreatePacket("aa:bb:cc:dd:ee:ff");
        var result = await new SaveSensorPacketHandler(
            writeContext,
            channel,
            cache,
            dispatcher
        ).HandleAsync(new SaveSensorPacketRequest(packet, DeviceType.Node));

        result.IsSuccess.ShouldBeTrue();

        await using var readContext = harness.CreateContext();
        readContext.Sensors.Count().ShouldBe(1);
        readContext.SensorPackets.Single().SensorId.ShouldBe(existingSensorId);

        A.CallTo(() => dispatcher.PublishAsync(A<SensorChangedEvent>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task DeletedSensor_IsRestoredAndPublished()
    {
        using var harness = new KelvinContextHarness();
        await using (var context = harness.CreateContext())
        {
            context.Sensors.Add(
                new Sensor
                {
                    MacAddress = "aa:bb:cc:dd:ee:ff",
                    Enabled = true,
                    DeletedAt = DateTimeOffset.UtcNow,
                }
            );
            await context.SaveChangesAsync();
        }

        await using var writeContext = harness.CreateContext();
        var dispatcher = A.Fake<IDispatcher>();
        var result = await new SaveSensorPacketHandler(
            writeContext,
            A.Fake<ISensorPacketChannel>(),
            A.Fake<IMemoryCache>(),
            dispatcher
        ).HandleAsync(
            new SaveSensorPacketRequest(CreatePacket("aa:bb:cc:dd:ee:ff"), DeviceType.Node)
        );

        result.IsSuccess.ShouldBeTrue();

        await using var readContext = harness.CreateContext();
        readContext.Sensors.Single().DeletedAt.ShouldBeNull();
        A.CallTo(() =>
                dispatcher.PublishAsync(
                    A<SensorChangedEvent>.That.Matches(e => e.Sensor.DeletedAt == null),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PublishesSavedPacketToChannel()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var channel = A.Fake<ISensorPacketChannel>();
        var cache = A.Fake<IMemoryCache>();

        var packet = CreatePacket("aa:bb:cc:dd:ee:ff");
        await new SaveSensorPacketHandler(
            context,
            channel,
            cache,
            A.Fake<IDispatcher>()
        ).HandleAsync(new SaveSensorPacketRequest(packet, DeviceType.Node));

        A.CallTo(() => channel.WriteAsync(packet, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task NullBattery_PersistsAndPublishesPacket()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var channel = A.Fake<ISensorPacketChannel>();
        var cache = A.Fake<IMemoryCache>();

        var packet = CreatePacket("aa:bb:cc:dd:ee:ff", null);
        var result = await new SaveSensorPacketHandler(
            context,
            channel,
            cache,
            A.Fake<IDispatcher>()
        ).HandleAsync(new SaveSensorPacketRequest(packet, DeviceType.Node));

        result.IsSuccess.ShouldBeTrue();

        await using var readContext = harness.CreateContext();
        var savedPacket = readContext.SensorPackets.Single();
        savedPacket.BatteryLevelPercentage.ShouldBeNull();

        A.CallTo(() => channel.WriteAsync(packet, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(DeviceType.Node, true, true, false)]
    [InlineData(DeviceType.Hmi, true, true, false)]
    [InlineData(DeviceType.Kiosk, true, false, true)]
    public async Task NewSensor_AppliesCapabilitiesForDeviceType(
        DeviceType deviceType,
        bool expectedHasHumiditySensor,
        bool expectedHasBattery,
        bool expectedHasCO2Sensor
    )
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var channel = A.Fake<ISensorPacketChannel>();
        var cache = A.Fake<IMemoryCache>();

        var packet = CreatePacket("aa:bb:cc:dd:ee:ff");
        var result = await new SaveSensorPacketHandler(
            context,
            channel,
            cache,
            A.Fake<IDispatcher>()
        ).HandleAsync(new SaveSensorPacketRequest(packet, deviceType));

        result.IsSuccess.ShouldBeTrue();

        await using var readContext = harness.CreateContext();
        var sensor = readContext.Sensors.Single();
        sensor.HasHumiditySensor.ShouldBe(expectedHasHumiditySensor);
        sensor.HasBattery.ShouldBe(expectedHasBattery);
        sensor.HasCO2Sensor.ShouldBe(expectedHasCO2Sensor);
    }

    [Fact]
    public async Task NewHmiSensor_LinksHmiRecordBySensorId()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var channel = A.Fake<ISensorPacketChannel>();
        var cache = A.Fake<IMemoryCache>();

        var packet = CreatePacket("aa:bb:cc:dd:ee:ff");
        var result = await new SaveSensorPacketHandler(
            context,
            channel,
            cache,
            A.Fake<IDispatcher>()
        ).HandleAsync(new SaveSensorPacketRequest(packet, DeviceType.Hmi));

        result.IsSuccess.ShouldBeTrue();

        await using var readContext = harness.CreateContext();
        var sensor = readContext.Sensors.Single();
        var hmi = readContext.Hmis.Single();
        hmi.MacAddress.ShouldBe("aa:bb:cc:dd:ee:ff");
        hmi.SensorId.ShouldBe(sensor.Id);
    }

    [Fact]
    public async Task ExistingUnlinkedHmi_GetsLinkedWhenItsSensorIsCreated()
    {
        using var harness = new KelvinContextHarness();
        await using (var seedContext = harness.CreateContext())
        {
            // Registered earlier via its command channel, before its first reading ever arrived.
            seedContext.Hmis.Add(new Hmi { MacAddress = "aa:bb:cc:dd:ee:ff" });
            await seedContext.SaveChangesAsync();
        }

        await using var context = harness.CreateContext();
        var channel = A.Fake<ISensorPacketChannel>();
        var cache = A.Fake<IMemoryCache>();

        var packet = CreatePacket("aa:bb:cc:dd:ee:ff");
        var result = await new SaveSensorPacketHandler(
            context,
            channel,
            cache,
            A.Fake<IDispatcher>()
        ).HandleAsync(new SaveSensorPacketRequest(packet, DeviceType.Hmi));

        result.IsSuccess.ShouldBeTrue();

        await using var readContext = harness.CreateContext();
        var sensor = readContext.Sensors.Single();
        readContext.Hmis.Single().SensorId.ShouldBe(sensor.Id);
    }
}
