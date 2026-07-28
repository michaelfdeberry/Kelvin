using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Thermostat;

/// <summary>
/// Tests for <see cref="GetThermostatHandler"/>, including the auto-create-on-first-read behavior, the eager
/// loading of set points/schedules, and the 24-hour <see cref="IMemoryCache"/> layer.
/// </summary>
public class GetThermostatTests
{
    [Fact]
    public async Task NoThermostatExists_CreatesDefaultThermostatAndPersistsIt()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());

        var result = await new GetThermostatHandler(context, cache).HandleAsync(
            new GetThermostatRequest()
        );

        var thermostat = result.Value.ShouldNotBeNull().Thermostat;
        thermostat.Mode.ShouldBe(RunMode.Disabled);
        thermostat.FanEnabled.ShouldBeFalse();
        thermostat.HysteresisC.ShouldBe(0.6f);

        await using var readContext = harness.CreateContext();
        readContext.Thermostats.Count().ShouldBe(1);
    }

    [Fact]
    public async Task IncludesSetPointsAndSchedules()
    {
        using var harness = new KelvinContextHarness();
        await using (var context = harness.CreateContext())
        {
            var thermostat = new Models.Thermostat { Mode = RunMode.Heating, FanEnabled = false };
            thermostat.SetPoints.Add(
                new SetPoint { Type = RunType.Heating, TargetTemperatureC = 21f }
            );
            thermostat.Schedules.Add(
                new Schedule
                {
                    Type = RunType.Heating,
                    Enabled = true,
                    StartTime = new TimeOnly(6, 0),
                    EndTime = new TimeOnly(22, 0),
                    TargetTemperatureC = 21f,
                }
            );
            context.Thermostats.Add(thermostat);
            await context.SaveChangesAsync();
        }

        await using var readContext = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var result = await new GetThermostatHandler(readContext, cache).HandleAsync(
            new GetThermostatRequest()
        );

        var thermostat2 = result.Value.ShouldNotBeNull().Thermostat;
        thermostat2.SetPoints.ShouldHaveSingleItem();
        thermostat2.Schedules.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SecondCall_UsesCache_DoesNotReflectLaterChanges()
    {
        using var harness = new KelvinContextHarness();
        var cache = new MemoryCache(new MemoryCacheOptions());

        Guid thermostatId;
        await using (var firstContext = harness.CreateContext())
        {
            var firstResult = await new GetThermostatHandler(firstContext, cache).HandleAsync(
                new GetThermostatRequest()
            );
            thermostatId = firstResult.Value.ShouldNotBeNull().Thermostat.Id;
        }

        // Mutate the row directly, bypassing the cache, to prove the second call never re-queries.
        await using (var mutateContext = harness.CreateContext())
        {
            var thermostat = mutateContext.Thermostats.Single();
            thermostat.Mode = RunMode.Cooling;
            await mutateContext.SaveChangesAsync();
        }

        await using var secondContext = harness.CreateContext();
        var secondResult = await new GetThermostatHandler(secondContext, cache).HandleAsync(
            new GetThermostatRequest()
        );

        var thermostat2 = secondResult.Value.ShouldNotBeNull().Thermostat;
        thermostat2.Id.ShouldBe(thermostatId);
        thermostat2.Mode.ShouldBe(RunMode.Disabled);
    }
}
