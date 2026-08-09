using FakeItEasy;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Thermostat;

/// <summary>
/// Tests for <see cref="UpdateThermostatHandler"/>, focused on which <see cref="ControlMessage"/>s are sent to
/// <see cref="IControlChannel"/> for each mode transition.
/// </summary>
public class UpdateThermostatTests
{
    private static (IControlChannel Channel, List<ControlMessage> Sent) CreateFakeControlChannel()
    {
        var sent = new List<ControlMessage>();
        var channel = A.Fake<IControlChannel>();
        A.CallTo(() => channel.WriteAsync(A<ControlMessage>._, A<CancellationToken>._))
            .Invokes((ControlMessage message, CancellationToken _) => sent.Add(message));

        return (channel, sent);
    }

    [Fact]
    public async Task NoThermostatExists_ReturnsFailure_AndSendsNoMessages()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var (channel, sent) = CreateFakeControlChannel();

        var result = await new UpdateThermostatHandler(context, cache, channel).HandleAsync(
            new UpdateThermostatRequest(RunMode.Heating, false)
        );

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UpdateThermostatErrors.ThermostatNotFound);
        sent.ShouldBeEmpty();
    }

    private static async Task<KelvinContextHarness> HarnessWithThermostatAsync()
    {
        var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        context.Thermostats.Add(
            new Models.Thermostat { Mode = RunMode.Disabled, FanEnabled = false }
        );
        await context.SaveChangesAsync();
        return harness;
    }

    [Fact]
    public async Task ModeDisabled_SendsOnlyDisableMessage()
    {
        using var harness = await HarnessWithThermostatAsync();
        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var (channel, sent) = CreateFakeControlChannel();

        var result = await new UpdateThermostatHandler(context, cache, channel).HandleAsync(
            new UpdateThermostatRequest(RunMode.Disabled, false)
        );

        result.IsSuccess.ShouldBeTrue();
        sent.ShouldHaveSingleItem().Context.State.ShouldBe(ControlState.Disable);
    }

    [Fact]
    public async Task ModeOff_SendsEnableThenDwellInOrder()
    {
        using var harness = await HarnessWithThermostatAsync();
        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var (channel, sent) = CreateFakeControlChannel();

        var result = await new UpdateThermostatHandler(context, cache, channel).HandleAsync(
            new UpdateThermostatRequest(RunMode.Off, false)
        );

        result.IsSuccess.ShouldBeTrue();
        sent.Count.ShouldBe(2);
        sent[0].Context.State.ShouldBe(ControlState.Enable);
        sent[1].Context.State.ShouldBe(ControlState.Dwell);
    }

    [Theory]
    [InlineData(RunMode.Heating)]
    [InlineData(RunMode.Cooling)]
    [InlineData(RunMode.Automatic)]
    public async Task ActiveModes_SendOnlyEnableMessage(RunMode mode)
    {
        using var harness = await HarnessWithThermostatAsync();
        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var (channel, sent) = CreateFakeControlChannel();

        var result = await new UpdateThermostatHandler(context, cache, channel).HandleAsync(
            new UpdateThermostatRequest(mode, false)
        );

        result.IsSuccess.ShouldBeTrue();
        sent.ShouldHaveSingleItem().Context.State.ShouldBe(ControlState.Enable);
    }

    [Fact]
    public async Task UpdatesModeAndFanEnabledInDatabase()
    {
        using var harness = await HarnessWithThermostatAsync();
        await using (var context = harness.CreateContext())
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            var (channel, _) = CreateFakeControlChannel();

            var result = await new UpdateThermostatHandler(context, cache, channel).HandleAsync(
                new UpdateThermostatRequest(RunMode.Heating, true)
            );
            result.IsSuccess.ShouldBeTrue();
        }

        await using var readContext = harness.CreateContext();
        var thermostat = readContext.Thermostats.Single();
        thermostat.Mode.ShouldBe(RunMode.Heating);
        thermostat.FanEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task ClearsThermostatCache()
    {
        using var harness = await HarnessWithThermostatAsync();
        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set(
            ThermostatCache.Key,
            new GetThermostatResponse(
                new Models.Thermostat { Mode = RunMode.Disabled, FanEnabled = false }
            ),
            TimeSpan.FromHours(24)
        );
        var (channel, _) = CreateFakeControlChannel();

        var result = await new UpdateThermostatHandler(context, cache, channel).HandleAsync(
            new UpdateThermostatRequest(RunMode.Heating, false)
        );

        result.IsSuccess.ShouldBeTrue();
        cache.TryGetValue(ThermostatCache.Key, out _).ShouldBeFalse();
    }
}
