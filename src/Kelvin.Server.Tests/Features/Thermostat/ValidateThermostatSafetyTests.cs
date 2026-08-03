using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Thermostat;

public class ValidateThermostatSafetyTests
{
    [Fact]
    public async Task DuplicateSetPointType_ReturnsFailure()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        context.Thermostats.Add(
            new Models.Thermostat { Mode = RunMode.Automatic, FanEnabled = false }
        );
        await context.SaveChangesAsync();

        var handler = new ValidateThermostatSafetyHandler(context);
        var request = new ValidateThermostatSafetyRequest(
            new ThermostatProjection(
                HysteresisC: 0.6f,
                SetPoints:
                [
                    new SetPointProjection(null, RunType.Heating, 20f),
                    new SetPointProjection(null, RunType.Heating, 19f),
                ],
                Schedules: []
            )
        );

        var result = await handler.HandleAsync(request);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ValidateThermostatSafetyErrors.DuplicateSetPointType);
    }

    [Fact]
    public async Task OverlappingSchedulesOfSameType_ReturnsFailure()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        context.Thermostats.Add(
            new Models.Thermostat { Mode = RunMode.Automatic, FanEnabled = false }
        );
        await context.SaveChangesAsync();

        var handler = new ValidateThermostatSafetyHandler(context);
        var request = new ValidateThermostatSafetyRequest(
            new ThermostatProjection(
                HysteresisC: 0.6f,
                SetPoints: [new SetPointProjection(null, RunType.Heating, 20f)],
                Schedules:
                [
                    new ScheduleProjection(
                        null,
                        RunType.Heating,
                        new TimeOnly(8, 0),
                        new TimeOnly(10, 0),
                        19f
                    ),
                    new ScheduleProjection(
                        null,
                        RunType.Heating,
                        new TimeOnly(9, 0),
                        new TimeOnly(11, 0),
                        18f
                    ),
                ]
            )
        );

        var result = await handler.HandleAsync(request);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ValidateThermostatSafetyErrors.OverlappingSchedulesSameType);
    }

    [Fact]
    public async Task UnsafeActivationOverlap_ReturnsFailure()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        context.Thermostats.Add(
            new Models.Thermostat
            {
                Mode = RunMode.Automatic,
                FanEnabled = false,
                HeatingLockoutC = 5f,
                CoolingLockoutC = 5f,
            }
        );
        await context.SaveChangesAsync();

        var handler = new ValidateThermostatSafetyHandler(context);
        var request = new ValidateThermostatSafetyRequest(
            new ThermostatProjection(
                HysteresisC: 0.6f,
                SetPoints:
                [
                    new SetPointProjection(null, RunType.Heating, 20f),
                    new SetPointProjection(null, RunType.Cooling, 24f),
                ],
                Schedules:
                [
                    new ScheduleProjection(
                        null,
                        RunType.Heating,
                        new TimeOnly(8, 0),
                        new TimeOnly(10, 0),
                        20f
                    ),
                    new ScheduleProjection(
                        null,
                        RunType.Cooling,
                        new TimeOnly(9, 0),
                        new TimeOnly(11, 0),
                        24f
                    ),
                ]
            )
        );

        var result = await handler.HandleAsync(request);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ValidateThermostatSafetyErrors.UnsafeActivationOverlap);
    }

    [Fact]
    public async Task SafeProjection_ReturnsSuccess()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        context.Thermostats.Add(
            new Models.Thermostat
            {
                Mode = RunMode.Automatic,
                FanEnabled = false,
                HeatingLockoutC = 5f,
                CoolingLockoutC = 25f,
            }
        );
        await context.SaveChangesAsync();

        var handler = new ValidateThermostatSafetyHandler(context);
        var request = new ValidateThermostatSafetyRequest(
            new ThermostatProjection(
                HysteresisC: 0.6f,
                SetPoints:
                [
                    new SetPointProjection(null, RunType.Heating, 20f),
                    new SetPointProjection(null, RunType.Cooling, 24f),
                ],
                Schedules:
                [
                    new ScheduleProjection(
                        null,
                        RunType.Heating,
                        new TimeOnly(22, 0),
                        new TimeOnly(6, 0),
                        18f
                    ),
                ]
            )
        );

        var result = await handler.HandleAsync(request);

        result.IsSuccess.ShouldBeTrue();
    }
}
