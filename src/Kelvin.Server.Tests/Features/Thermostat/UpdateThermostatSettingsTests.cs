using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Thermostat;

public class UpdateThermostatSettingsTests
{
    [Fact]
    public async Task NoThermostat_ReturnsFailure()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var validator = A.Fake<IHandler<ValidateThermostatSafetyRequest>>();

        var result = await new UpdateThermostatSettingsHandler(
            context,
            cache,
            validator
        ).HandleAsync(new UpdateThermostatSettingsRequest(null, null, [], []));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UpdateThermostatSettingsErrors.ThermostatNotFound);
    }

    [Fact]
    public async Task SafeUpdate_ReplacesSetPointsAndSchedules_AndClearsCache()
    {
        using var harness = new KelvinContextHarness();
        Guid keptSetPointId;
        Guid removedSetPointId;
        Guid keptScheduleId;
        Guid removedScheduleId;
        await using (var seedContext = harness.CreateContext())
        {
            var seededThermostat = new Models.Thermostat
            {
                Mode = RunMode.Automatic,
                FanEnabled = false,
            };
            var keptSetPoint = new SetPoint { Type = RunType.Heating, TargetTemperatureC = 20f };
            var removedSetPoint = new SetPoint { Type = RunType.Cooling, TargetTemperatureC = 24f };
            var keptSchedule = new Schedule
            {
                Type = RunType.Heating,
                StartTime = new TimeOnly(6, 0),
                EndTime = new TimeOnly(8, 0),
                TargetTemperatureC = 19f,
            };
            var removedSchedule = new Schedule
            {
                Type = RunType.Cooling,
                StartTime = new TimeOnly(18, 0),
                EndTime = new TimeOnly(20, 0),
                TargetTemperatureC = 23f,
            };

            seededThermostat.SetPoints.Add(keptSetPoint);
            seededThermostat.SetPoints.Add(removedSetPoint);
            seededThermostat.Schedules.Add(keptSchedule);
            seededThermostat.Schedules.Add(removedSchedule);
            seedContext.Thermostats.Add(seededThermostat);
            await seedContext.SaveChangesAsync();

            keptSetPointId = keptSetPoint.Id;
            removedSetPointId = removedSetPoint.Id;
            keptScheduleId = keptSchedule.Id;
            removedScheduleId = removedSchedule.Id;
        }

        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set(ThermostatCache.Key, new object());
        var validator = A.Fake<IHandler<ValidateThermostatSafetyRequest>>();
        A.CallTo(() =>
                validator.HandleAsync(A<ValidateThermostatSafetyRequest>._, A<CancellationToken>._)
            )
            .Returns(Result.Success());

        var result = await new UpdateThermostatSettingsHandler(
            context,
            cache,
            validator
        ).HandleAsync(
            new UpdateThermostatSettingsRequest(
                12f,
                28f,
                [
                    new SetPointInput(keptSetPointId, RunType.Heating, 21f),
                    new SetPointInput(null, RunType.Cooling, 25f),
                ],
                [
                    new ScheduleInput(
                        keptScheduleId,
                        RunType.Heating,
                        new TimeOnly(6, 30),
                        new TimeOnly(8, 30),
                        19.5f
                    ),
                ]
            )
        );

        result.IsSuccess.ShouldBeTrue();
        cache.TryGetValue(ThermostatCache.Key, out _).ShouldBeFalse();

        var thermostat = context.Thermostats.Single();
        thermostat.HeatingLockoutC.ShouldBe(12f);
        thermostat.CoolingLockoutC.ShouldBe(28f);

        var setPoints = context.SetPoints.Where(sp => sp.DeletedAt == null).ToList();
        setPoints.Count.ShouldBe(2);
        setPoints.ShouldContain(sp => sp.Id == keptSetPointId && sp.TargetTemperatureC == 21f);
        setPoints.ShouldNotContain(sp => sp.Id == removedSetPointId);
        context.SetPoints.Single(sp => sp.Id == removedSetPointId).DeletedAt.ShouldNotBeNull();

        var schedules = context.Schedules.Where(s => s.DeletedAt == null).ToList();
        schedules.Count.ShouldBe(1);
        schedules.ShouldContain(s => s.Id == keptScheduleId && s.StartTime == new TimeOnly(6, 30));
        schedules.ShouldNotContain(s => s.Id == removedScheduleId);
        context.Schedules.Single(s => s.Id == removedScheduleId).DeletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task UnsafeUpdate_ReturnsValidationFailure_AndDoesNotPersist()
    {
        using var harness = new KelvinContextHarness();
        await using (var seedContext = harness.CreateContext())
        {
            var thermostat = new Models.Thermostat { Mode = RunMode.Automatic, FanEnabled = false };
            thermostat.SetPoints.Add(
                new SetPoint { Type = RunType.Heating, TargetTemperatureC = 20f }
            );
            seedContext.Thermostats.Add(thermostat);
            await seedContext.SaveChangesAsync();
        }

        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var validator = A.Fake<IHandler<ValidateThermostatSafetyRequest>>();
        A.CallTo(() =>
                validator.HandleAsync(A<ValidateThermostatSafetyRequest>._, A<CancellationToken>._)
            )
            .Returns(Result.Failure(ValidateThermostatSafetyErrors.UnsafeTargetOverlap));

        var result = await new UpdateThermostatSettingsHandler(
            context,
            cache,
            validator
        ).HandleAsync(
            new UpdateThermostatSettingsRequest(
                12f,
                null,
                [new SetPointInput(null, RunType.Cooling, 99f)],
                []
            )
        );

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ValidateThermostatSafetyErrors.UnsafeTargetOverlap);

        // Verify via a fresh context - the handler mutates the tracked thermostat's lockout fields in-memory
        // before validating, so re-reading through the same context would misleadingly show that change.
        await using var verifyContext = harness.CreateContext();
        verifyContext.SetPoints.Count().ShouldBe(1);
        verifyContext.Thermostats.Single().HeatingLockoutC.ShouldBeNull();
    }
}
