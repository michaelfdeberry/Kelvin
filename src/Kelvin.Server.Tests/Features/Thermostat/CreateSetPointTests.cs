using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Thermostat;

public class CreateSetPointTests
{
    [Fact]
    public async Task NoThermostat_ReturnsFailure()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var validator = A.Fake<IHandler<ValidateThermostatSafetyRequest>>();
        A.CallTo(() =>
                validator.HandleAsync(A<ValidateThermostatSafetyRequest>._, A<CancellationToken>._)
            )
            .Returns(Result.Success());

        var result = await new CreateSetPointHandler(context, cache, validator).HandleAsync(
            new CreateSetPointRequest(RunType.Heating, 20f, 5f)
        );

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CreateSetPointErrors.ThermostatNotFound);
    }

    [Fact]
    public async Task SafeCreate_PersistsAndClearsCache()
    {
        using var harness = new KelvinContextHarness();
        await using (var seedContext = harness.CreateContext())
        {
            seedContext.Thermostats.Add(
                new Models.Thermostat { Mode = RunMode.Automatic, FanEnabled = false }
            );
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

        var result = await new CreateSetPointHandler(context, cache, validator).HandleAsync(
            new CreateSetPointRequest(RunType.Heating, 21f, 5f)
        );

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull().TargetTemperatureC.ShouldBe(21f);

        cache.TryGetValue(ThermostatCache.Key, out _).ShouldBeFalse();
        context.SetPoints.Count().ShouldBe(1);
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
            .Returns(Result.Failure(ValidateThermostatSafetyErrors.DuplicateSetPointType));

        var result = await new CreateSetPointHandler(context, cache, validator).HandleAsync(
            new CreateSetPointRequest(RunType.Heating, 21f, 5f)
        );

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ValidateThermostatSafetyErrors.DuplicateSetPointType);
        context.SetPoints.Count().ShouldBe(0);
    }
}
