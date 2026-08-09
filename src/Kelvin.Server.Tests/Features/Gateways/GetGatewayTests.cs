using Kelvin.Server.Features.Gateways;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Gateways;

/// <summary>
/// Tests for <see cref="GetGatewayHandler"/>, including the 24-hour <see cref="IMemoryCache"/> layer in front of
/// the database read.
/// </summary>
public class GetGatewayTests
{
    [Fact]
    public async Task NoGatewayRegistered_ReturnsNotFound()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());

        var result = await new GetGatewayHandler(context, cache).HandleAsync(
            new GetGatewayRequest()
        );

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GetGatewayErrors.NotFound);
    }

    [Fact]
    public async Task ReturnsStoredGatewayFields()
    {
        using var harness = new KelvinContextHarness();
        await using (var context = harness.CreateContext())
        {
            context.Gateways.Add(
                new Gateway
                {
                    MacAddress = "aa:bb:cc:dd:ee:ff",
                    HeatingPin = 17,
                    FanPin = 27,
                    CoolingPin = 22,
                    ControlPin = 23,
                    MinimumOffDurationMinutes = 5,
                    MinimumOnDurationMinutes = 3,
                }
            );
            await context.SaveChangesAsync();
        }

        await using var readContext = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var result = await new GetGatewayHandler(readContext, cache).HandleAsync(
            new GetGatewayRequest()
        );

        var gateway = result.Value.ShouldNotBeNull();
        gateway.MacAddress.ShouldBe("aa:bb:cc:dd:ee:ff");
        gateway.HeatingPin.ShouldBe(17);
        gateway.FanPin.ShouldBe(27);
        gateway.CoolingPin.ShouldBe(22);
        gateway.ControlPin.ShouldBe(23);
        gateway.MinimumOffDurationMinutes.ShouldBe(5);
        gateway.MinimumOnDurationMinutes.ShouldBe(3);
    }

    [Fact]
    public async Task SecondCall_UsesCache_DoesNotReQueryDatabase()
    {
        using var harness = new KelvinContextHarness();
        await using (var context = harness.CreateContext())
        {
            context.Gateways.Add(new Gateway { MacAddress = "aa:bb:cc:dd:ee:ff" });
            await context.SaveChangesAsync();
        }

        var cache = new MemoryCache(new MemoryCacheOptions());

        await using (var firstContext = harness.CreateContext())
        {
            var firstResult = await new GetGatewayHandler(firstContext, cache).HandleAsync(
                new GetGatewayRequest()
            );
            firstResult.Value.ShouldNotBeNull().MacAddress.ShouldBe("aa:bb:cc:dd:ee:ff");
        }

        // Mutate the row directly, bypassing the cache, to prove the second call never touches the database.
        await using (var mutateContext = harness.CreateContext())
        {
            var gateway = mutateContext.Gateways.Single();
            gateway.MacAddress = "11:22:33:44:55:66";
            await mutateContext.SaveChangesAsync();
        }

        await using var secondContext = harness.CreateContext();
        var secondResult = await new GetGatewayHandler(secondContext, cache).HandleAsync(
            new GetGatewayRequest()
        );

        secondResult.Value.ShouldNotBeNull().MacAddress.ShouldBe("aa:bb:cc:dd:ee:ff");
    }
}
