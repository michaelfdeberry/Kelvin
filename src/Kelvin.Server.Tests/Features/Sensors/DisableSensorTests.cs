using FakeItEasy;
using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Sensors;

public class DisableSensorTests
{
    [Fact]
    public async Task DisablesSensorClearsCacheAndNotifies()
    {
        using var harness = new KelvinContextHarness();
        await using var seedContext = harness.CreateContext();
        var sensor = new Sensor { Enabled = true };
        seedContext.Sensors.Add(sensor);
        await seedContext.SaveChangesAsync();
        var sensorId = sensor.Id;

        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set(SensorsCache.Key, new object());
        var (hub, client) = SensorTestHelpers.CreateControlHub();

        var result = await new DisableSensorHandler(context, cache, hub).HandleAsync(
            new DisableSensorRequest(sensorId)
        );

        result.IsSuccess.ShouldBeTrue();
        cache.TryGetValue(SensorsCache.Key, out _).ShouldBeFalse();
        FakeItEasy.A.CallTo(() => client.SensorsStateChanged()).MustHaveHappenedOnceExactly();
        (await harness.CreateContext().Sensors.FindAsync(sensorId))!.Enabled.ShouldBeFalse();
    }
}
