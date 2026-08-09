using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Thermostat;

public class UpdateScheduleTests
{
    [Fact]
    public async Task ScheduleNotFound_ReturnsFailure()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var validator = A.Fake<IHandler<ValidateThermostatSafetyRequest>>();

        var result = await new UpdateScheduleHandler(context, cache, validator).HandleAsync(
            new UpdateScheduleRequest(
                Guid.NewGuid(),
                RunType.Heating,
                new TimeOnly(22, 0),
                new TimeOnly(6, 0),
                18f
            )
        );

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UpdateScheduleErrors.ScheduleNotFound);
    }

    [Fact]
    public async Task SafeUpdate_UpdatesAndClearsCache()
    {
        using var harness = new KelvinContextHarness();
        Guid scheduleId;
        await using (var seedContext = harness.CreateContext())
        {
            var thermostat = new Models.Thermostat
            {
                Mode = RunMode.Automatic,
                FanEnabled = false,
                HeatingLockoutC = 4f,
            };
            var schedule = new Schedule
            {
                Type = RunType.Heating,
                StartTime = new TimeOnly(22, 0),
                EndTime = new TimeOnly(6, 0),
                TargetTemperatureC = 18f,
            };
            thermostat.Schedules.Add(schedule);
            seedContext.Thermostats.Add(thermostat);
            await seedContext.SaveChangesAsync();
            scheduleId = schedule.Id;
        }

        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set(ThermostatCache.Key, new object());
        var validator = A.Fake<IHandler<ValidateThermostatSafetyRequest>>();
        A.CallTo(() =>
                validator.HandleAsync(A<ValidateThermostatSafetyRequest>._, A<CancellationToken>._)
            )
            .Returns(Result.Success());

        var result = await new UpdateScheduleHandler(context, cache, validator).HandleAsync(
            new UpdateScheduleRequest(
                scheduleId,
                RunType.Heating,
                new TimeOnly(21, 0),
                new TimeOnly(7, 0),
                19f
            )
        );

        result.IsSuccess.ShouldBeTrue();
        cache.TryGetValue(ThermostatCache.Key, out _).ShouldBeFalse();

        var updatedSchedule = context.Schedules.Single();
        updatedSchedule.StartTime.ShouldBe(new TimeOnly(21, 0));
        updatedSchedule.EndTime.ShouldBe(new TimeOnly(7, 0));
        updatedSchedule.TargetTemperatureC.ShouldBe(19f);
    }

    [Fact]
    public async Task UnsafeUpdate_ReturnsValidationFailure_AndDoesNotPersist()
    {
        using var harness = new KelvinContextHarness();
        Guid scheduleId;
        await using (var seedContext = harness.CreateContext())
        {
            var thermostat = new Models.Thermostat
            {
                Mode = RunMode.Automatic,
                FanEnabled = false,
                HeatingLockoutC = 99f,
            };
            var schedule = new Schedule
            {
                Type = RunType.Heating,
                StartTime = new TimeOnly(22, 0),
                EndTime = new TimeOnly(6, 0),
                TargetTemperatureC = 18f,
            };
            thermostat.Schedules.Add(schedule);
            seedContext.Thermostats.Add(thermostat);
            await seedContext.SaveChangesAsync();
            scheduleId = schedule.Id;
        }

        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var validator = A.Fake<IHandler<ValidateThermostatSafetyRequest>>();
        A.CallTo(() =>
                validator.HandleAsync(A<ValidateThermostatSafetyRequest>._, A<CancellationToken>._)
            )
            .Returns(Result.Failure(ValidateThermostatSafetyErrors.UnsafeActivationOverlap));

        var result = await new UpdateScheduleHandler(context, cache, validator).HandleAsync(
            new UpdateScheduleRequest(
                scheduleId,
                RunType.Heating,
                new TimeOnly(22, 0),
                new TimeOnly(6, 0),
                99f
            )
        );

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ValidateThermostatSafetyErrors.UnsafeActivationOverlap);

        var unchangedSchedule = context.Schedules.Single();
        unchangedSchedule.TargetTemperatureC.ShouldBe(18f);
    }
}
