using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Sensors;

public class GetSensorsTests
{
    [Fact]
    public async Task ExcludesDeletedSensorsAndCachesResponse()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        context.Sensors.AddRange(
            new Sensor { Name = "Active" },
            new Sensor { Name = "Deleted", DeletedAt = DateTimeOffset.UtcNow }
        );
        await context.SaveChangesAsync();
        var cache = new MemoryCache(new MemoryCacheOptions());

        var result = await new GetSensorsHandler(context, cache).HandleAsync(new GetSensorsRequest());

        result.Value!.Sensors.ShouldHaveSingleItem().Name.ShouldBe("Active");
        cache.TryGetValue(SensorsCache.Key, out GetSensorsResponse? cached).ShouldBeTrue();
        cached.ShouldBeSameAs(result.Value);
    }
}