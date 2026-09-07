using FakeItEasy;
using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Sensors;

public class UpdateSensorTests
{
    [Fact]
    public async Task PersistsChangesClearsCacheAndNotifies()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var sensor = new Sensor { Name = "Old", MacAddress = "old" };
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync();
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set(SensorsCache.Key, new object());
        var (hub, client) = SensorTestHelpers.CreateControlHub();

        var result = await new UpdateSensorHandler(context, cache, hub).HandleAsync(
            new UpdateSensorRequest(
                sensor.Id,
                new SensorRequest(sensor.Id, "New", "new", true, true, true, 1.5f, 2.5f, 3)
            )
        );

        result.IsSuccess.ShouldBeTrue();
        cache.TryGetValue(SensorsCache.Key, out _).ShouldBeFalse();
        FakeItEasy.A.CallTo(() => client.SensorsStateChanged()).MustHaveHappenedOnceExactly();
        var updated = (await harness.CreateContext().Sensors.FindAsync(sensor.Id))!;
        updated.Name.ShouldBe("New");
        updated.MacAddress.ShouldBe("new");
        updated.HasBattery.ShouldBeTrue();
        updated.TemperatureCOffset.ShouldBe(1.5f);
        updated.CO2LevelPpmOffset.ShouldBe((short)3);
    }
}
