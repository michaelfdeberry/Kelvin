using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Thermostat;

public class UpdateSetPointTests
{
    [Fact]
    public async Task SetPointNotFound_ReturnsFailure()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var validator = A.Fake<IHandler<ValidateThermostatSafetyRequest>>();

        var result = await new UpdateSetPointHandler(context, cache, validator).HandleAsync(
            new UpdateSetPointRequest(Guid.NewGuid(), RunType.Heating, 20f)
        );

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UpdateSetPointErrors.SetPointNotFound);
    }

    [Fact]
    public async Task SafeUpdate_UpdatesAndClearsCache()
    {
        using var harness = new KelvinContextHarness();
        Guid setPointId;
        await using (var seedContext = harness.CreateContext())
        {
            var thermostat = new Models.Thermostat { Mode = RunMode.Automatic, FanEnabled = false };
            var setPoint = new SetPoint { Type = RunType.Heating, TargetTemperatureC = 20f };
            thermostat.SetPoints.Add(setPoint);
            seedContext.Thermostats.Add(thermostat);
            await seedContext.SaveChangesAsync();
            setPointId = setPoint.Id;
        }

        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set(ThermostatCache.Key, new object());
        var validator = A.Fake<IHandler<ValidateThermostatSafetyRequest>>();
        A.CallTo(() =>
                validator.HandleAsync(A<ValidateThermostatSafetyRequest>._, A<CancellationToken>._)
            )
            .Returns(Result.Success());

        var result = await new UpdateSetPointHandler(context, cache, validator).HandleAsync(
            new UpdateSetPointRequest(setPointId, RunType.Heating, 21f)
        );

        result.IsSuccess.ShouldBeTrue();
        cache.TryGetValue(ThermostatCache.Key, out _).ShouldBeFalse();

        var updatedSetPoint = context.SetPoints.Single();
        updatedSetPoint.TargetTemperatureC.ShouldBe(21f);
    }

    [Fact]
    public async Task UnsafeUpdate_ReturnsValidationFailure_AndDoesNotPersist()
    {
        using var harness = new KelvinContextHarness();
        Guid setPointId;
        await using (var seedContext = harness.CreateContext())
        {
            var thermostat = new Models.Thermostat { Mode = RunMode.Automatic, FanEnabled = false };
            var setPoint = new SetPoint { Type = RunType.Heating, TargetTemperatureC = 20f };
            thermostat.SetPoints.Add(setPoint);
            seedContext.Thermostats.Add(thermostat);
            await seedContext.SaveChangesAsync();
            setPointId = setPoint.Id;
        }

        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var validator = A.Fake<IHandler<ValidateThermostatSafetyRequest>>();
        A.CallTo(() =>
                validator.HandleAsync(A<ValidateThermostatSafetyRequest>._, A<CancellationToken>._)
            )
            .Returns(Result.Failure(ValidateThermostatSafetyErrors.UnsafeTargetOverlap));

        var result = await new UpdateSetPointHandler(context, cache, validator).HandleAsync(
            new UpdateSetPointRequest(setPointId, RunType.Heating, 99f)
        );

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ValidateThermostatSafetyErrors.UnsafeTargetOverlap);

        var unchangedSetPoint = context.SetPoints.Single();
        unchangedSetPoint.TargetTemperatureC.ShouldBe(20f);
    }
}
