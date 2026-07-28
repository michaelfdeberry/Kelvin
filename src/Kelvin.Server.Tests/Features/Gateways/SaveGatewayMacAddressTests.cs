using Kelvin.Server.Features.Gateways;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Gateways;

/// <summary>
/// Tests for <see cref="SaveGatewayMacAddressHandler"/>.
/// </summary>
public class SaveGatewayMacAddressTests
{
    [Fact]
    public async Task InvalidMacAddress_ReturnsFailure_AndDoesNotCreateAGateway()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());

        var result = await new SaveGatewayMacAddressHandler(context, cache).HandleAsync(
            new SaveGatewayMacAddressRequest("not-a-mac")
        );

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SaveGatewayMacAddressErrors.InvalidMacAddress);
        context.Gateways.Any().ShouldBeFalse();
    }

    [Fact]
    public async Task NoExistingGateway_CreatesNewGatewayWithMac()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());

        var result = await new SaveGatewayMacAddressHandler(context, cache).HandleAsync(
            new SaveGatewayMacAddressRequest("aa:bb:cc:dd:ee:ff")
        );

        result.IsSuccess.ShouldBeTrue();

        await using var readContext = harness.CreateContext();
        readContext.Gateways.Single().MacAddress.ShouldBe("aa:bb:cc:dd:ee:ff");
    }

    [Fact]
    public async Task ExistingGateway_UpdatesMacAddress()
    {
        using var harness = new KelvinContextHarness();
        await using (var context = harness.CreateContext())
        {
            context.Gateways.Add(new Gateway { MacAddress = "11:22:33:44:55:66" });
            await context.SaveChangesAsync();
        }

        await using var updateContext = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var result = await new SaveGatewayMacAddressHandler(updateContext, cache).HandleAsync(
            new SaveGatewayMacAddressRequest("aa:bb:cc:dd:ee:ff")
        );

        result.IsSuccess.ShouldBeTrue();

        await using var readContext = harness.CreateContext();
        readContext.Gateways.Single().MacAddress.ShouldBe("aa:bb:cc:dd:ee:ff");
    }

    [Fact]
    public async Task ClearsGatewayCache()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set(
            GatewayCache.Key,
            new GetGatewayResponse("11:22:33:44:55:66", null, null, null, null, null, null),
            TimeSpan.FromHours(24)
        );

        var result = await new SaveGatewayMacAddressHandler(context, cache).HandleAsync(
            new SaveGatewayMacAddressRequest("aa:bb:cc:dd:ee:ff")
        );

        result.IsSuccess.ShouldBeTrue();
        cache.TryGetValue(GatewayCache.Key, out _).ShouldBeFalse();
    }
}
