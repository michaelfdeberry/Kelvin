using Kelvin.Server.Features.Gateways;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Gateways;

/// <summary>
/// Tests for <see cref="UpdateGatewayHandler"/>.
/// </summary>
public class UpdateGatewayTests
{
    [Fact]
    public async Task NoGatewayRegistered_ReturnsFailure()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());

        var result = await new UpdateGatewayHandler(context, cache).HandleAsync(
            new UpdateGatewayRequest(17, 27, 22, 23, 5, 3)
        );

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UpdateGatewayErrors.NotFound);
    }

    [Fact]
    public async Task UpdatesPinAndDurationFields()
    {
        using var harness = new KelvinContextHarness();
        await using (var context = harness.CreateContext())
        {
            context.Gateways.Add(
                new Gateway
                {
                    MacAddress = "aa:bb:cc:dd:ee:ff",
                    HeatingPin = 1,
                    FanPin = 2,
                    CoolingPin = 3,
                    ControlPin = 4,
                    MinimumOffDurationMinutes = 5,
                    MinimumOnDurationMinutes = 3,
                }
            );
            await context.SaveChangesAsync();
        }

        await using var updateContext = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var result = await new UpdateGatewayHandler(updateContext, cache).HandleAsync(
            new UpdateGatewayRequest(
                HeatingPin: 17,
                FanPin: 27,
                CoolingPin: null,
                ControlPin: 23,
                MinimumOffDurationMinutes: 10,
                MinimumOnDurationMinutes: null
            )
        );

        result.IsSuccess.ShouldBeTrue();

        await using var readContext = harness.CreateContext();
        var gateway = readContext.Gateways.Single();
        gateway.HeatingPin.ShouldBe(17);
        gateway.FanPin.ShouldBe(27);
        // Nullable fields are always overwritten, including clearing a previously-assigned pin.
        gateway.CoolingPin.ShouldBeNull();
        gateway.ControlPin.ShouldBe(23);
        gateway.MinimumOffDurationMinutes.ShouldBe(10);
        gateway.MinimumOnDurationMinutes.ShouldBeNull();
    }

    [Fact]
    public async Task ClearsGatewayCache()
    {
        using var harness = new KelvinContextHarness();
        await using (var context = harness.CreateContext())
        {
            context.Gateways.Add(new Gateway { MacAddress = "aa:bb:cc:dd:ee:ff" });
            await context.SaveChangesAsync();
        }

        await using var updateContext = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set(
            GatewayCache.Key,
            new GetGatewayResponse("aa:bb:cc:dd:ee:ff", null, null, null, null, null, null),
            TimeSpan.FromHours(24)
        );

        var result = await new UpdateGatewayHandler(updateContext, cache).HandleAsync(
            new UpdateGatewayRequest(1, 2, 3, 4, 5, 6)
        );

        result.IsSuccess.ShouldBeTrue();
        cache.TryGetValue(GatewayCache.Key, out _).ShouldBeFalse();
    }
}
