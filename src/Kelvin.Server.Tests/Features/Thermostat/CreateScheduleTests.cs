using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Thermostat;

public class CreateScheduleTests
{
    [Fact]
    public async Task NoThermostat_ReturnsFailure()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var validator = A.Fake<IHandler<ValidateThermostatSafetyRequest>>();

        var result = await new CreateScheduleHandler(context, cache, validator).HandleAsync(
            new CreateScheduleRequest(RunType.Heating, new TimeOnly(22, 0), new TimeOnly(6, 0), 18f)
        );

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CreateScheduleErrors.ThermostatNotFound);
    }

    [Fact]
    public async Task SafeCreate_PersistsAndClearsCache()
    {
        using var harness = new KelvinContextHarness();
        await using (var seedContext = harness.CreateContext())
        {
            var thermostat = new Models.Thermostat { Mode = RunMode.Automatic, FanEnabled = false };
            thermostat.SetPoints.Add(
                new SetPoint { Type = RunType.Heating, TargetTemperatureC = 20f }
            );
            thermostat.SetPoints.Add(
                new SetPoint { Type = RunType.Cooling, TargetTemperatureC = 24f }
            );
            seedContext.Thermostats.Add(thermostat);
            await seedContext.SaveChangesAsync();
        }

        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set(ThermostatCache.Key, new object());
        var validator = A.Fake<IHandler<ValidateThermostatSafetyRequest>>();
        A.CallTo(() =>
                validator.HandleAsync(A<ValidateThermostatSafetyRequest>._, A<CancellationToken>._)
            )
            .Returns(Result.Success());

        var result = await new CreateScheduleHandler(context, cache, validator).HandleAsync(
            new CreateScheduleRequest(RunType.Heating, new TimeOnly(22, 0), new TimeOnly(6, 0), 18f)
        );

        result.IsSuccess.ShouldBeTrue();
        context.Schedules.Count().ShouldBe(1);
        cache.TryGetValue(ThermostatCache.Key, out _).ShouldBeFalse();
    }

    [Fact]
    public async Task UnsafeProjection_ReturnsValidationFailure()
    {
        using var harness = new KelvinContextHarness();
        await using (var seedContext = harness.CreateContext())
        {
            var thermostat = new Models.Thermostat { Mode = RunMode.Automatic, FanEnabled = false };
            seedContext.Thermostats.Add(thermostat);
            await seedContext.SaveChangesAsync();
        }

        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var validator = A.Fake<IHandler<ValidateThermostatSafetyRequest>>();
        A.CallTo(() =>
                validator.HandleAsync(A<ValidateThermostatSafetyRequest>._, A<CancellationToken>._)
            )
            .Returns(Result.Failure(ValidateThermostatSafetyErrors.OverlappingSchedulesSameType));

        var result = await new CreateScheduleHandler(context, cache, validator).HandleAsync(
            new CreateScheduleRequest(RunType.Heating, new TimeOnly(22, 0), new TimeOnly(6, 0), 18f)
        );

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ValidateThermostatSafetyErrors.OverlappingSchedulesSameType);
        context.Schedules.Count().ShouldBe(0);
    }
}
