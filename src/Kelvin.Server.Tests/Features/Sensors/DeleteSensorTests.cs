using FakeItEasy;
using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Sensors;

public class DeleteSensorTests
{
    [Fact]
    public async Task SoftDeletesSensorClearsCacheAndNotifies()
    {
        using var harness = new KelvinContextHarness();
        Guid sensorId;
        await using (var seedContext = harness.CreateContext())
        {
            var sensor = new Sensor { Name = "Kitchen" };
            seedContext.Sensors.Add(sensor);
            await seedContext.SaveChangesAsync();
            sensorId = sensor.Id;
        }

        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set(SensorsCache.Key, new object());
        var (hub, client) = SensorTestHelpers.CreateControlHub();

        var result = await new DeleteSensorHandler(context, cache, hub).HandleAsync(
            new DeleteSensorRequest(sensorId)
        );

        result.IsSuccess.ShouldBeTrue();
        cache.TryGetValue(SensorsCache.Key, out _).ShouldBeFalse();
        FakeItEasy.A.CallTo(() => client.SensorsStateChanged()).MustHaveHappenedOnceExactly();
        (await harness.CreateContext().Sensors.FindAsync(sensorId))!.DeletedAt.ShouldNotBeNull();
    }
}
