using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Sensors;

public class RestoreSensorTests
{
    [Fact]
    public async Task ClearsDeletedAtAndCache()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var sensor = new Sensor { DeletedAt = DateTimeOffset.UtcNow };
        context.Sensors.Add(sensor);
        await context.SaveChangesAsync();
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set(SensorsCache.Key, new object());

        var result = await new RestoreSensorHandler(context, cache).HandleAsync(new RestoreSensorRequest(sensor.Id));

        result.IsSuccess.ShouldBeTrue();
        cache.TryGetValue(SensorsCache.Key, out _).ShouldBeFalse();
        (await harness.CreateContext().Sensors.FindAsync(sensor.Id))!.DeletedAt.ShouldBeNull();
    }
}