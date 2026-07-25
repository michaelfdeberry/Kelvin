using Kelvin.Server.Application;
using Kelvin.Server.Features.GeoCoding;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Features.Weather;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="Kelvin.Server.Services.ThermostatService"/>, exercised entirely through its public
/// <see cref="Microsoft.Extensions.Hosting.BackgroundService"/> surface (StartAsync/StopAsync) via
/// <see cref="ThermostatServiceHarness"/>. All constructor dependencies (IControlChannel, IEnvironmentChannel,
/// IDispatcher) are FakeItEasy fakes.
/// </summary>
/// <remarks>
/// NOTE on the "direct Heating<->Cooling switch" safety guard in ProcessTemperature: reading the source,
/// `callForCooling` can only become true when `_activeCall` is already `Dwell` or `Cooling` (never `Heating`), and
/// symmetrically `callForHeating` can only become true when `_activeCall` is `Dwell` or `Heating` (never `Cooling`).
/// That means the guard clause checking for a direct Heating&lt;-&gt;Cooling flip is unreachable via the public
/// surface as currently written - it can never fire given how callForHeating/callForCooling are gated. It is not
/// tested here for that reason (asserting unreachable code would be misleading). The "simultaneous heating AND
/// cooling calls" guard below IS reachable (both can independently start from Dwell) and is tested.
/// </remarks>
public class ThermostatServiceTests
{
    [Fact]
    public async Task Disabled_Mode_WritesDisableOnly()
    {
        var harness = new ThermostatServiceHarness();
        harness.SetThermostat(ThermostatFixtures.CreateThermostat(RunMode.Disabled));

        await harness.StartAsync();
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(20));

        harness.WrittenMessages.ShouldBe([new ControlMessage(ControlState.Disable)]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task Off_Mode_WritesEnableThenDwell()
    {
        var harness = new ThermostatServiceHarness();
        harness.SetThermostat(ThermostatFixtures.CreateThermostat(RunMode.Off));

        await harness.StartAsync();
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(20));

        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task NoActiveSchedulesOrSetPoints_WritesEnableThenDwell()
    {
        var harness = new ThermostatServiceHarness();
        harness.SetThermostat(ThermostatFixtures.CreateThermostat(RunMode.Automatic));

        await harness.StartAsync();
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(20));

        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task ThermostatDispatchFailure_WritesNoMessage_AndLoopSurvivesForNextReading()
    {
        var harness = new ThermostatServiceHarness();
        harness.SetThermostatFailure(GetThermostatErrors.ThermostatNotFound);

        await harness.StartAsync();
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(20));

        harness.WrittenMessages.ShouldBeEmpty();

        // the loop should still be alive and process the next reading correctly once the dependency recovers
        harness.SetThermostat(ThermostatFixtures.CreateThermostat(RunMode.Off));
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(20));

        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task NullEnvironmentReading_WritesNoMessage_AndLoopSurvivesForNextReading()
    {
        var harness = new ThermostatServiceHarness();
        harness.SetThermostat(ThermostatFixtures.CreateThermostat(RunMode.Off));

        await harness.StartAsync();
        await harness.PushEnvironmentAsync(null!);

        harness.WrittenMessages.ShouldBeEmpty();

        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(20));

        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task Heating_TurnsOn_BelowTargetMinusHysteresis_ThenTurnsOff_AboveTargetPlusHysteresis()
    {
        var harness = new ThermostatServiceHarness();
        var setPoint = ThermostatFixtures.CreateSetPoint(RunType.Heating, targetTemperatureC: 20f);
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(RunMode.Heating, setPoints: [setPoint])
        );

        await harness.StartAsync();

        // 20 - 0.6 = 19.4; 19.0 <= 19.4 -> starts heating from Dwell
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(19.0));
        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Heating),
        ]);
        harness.WrittenMessages.Clear();

        // 20 + 0.6 = 20.6; 21.0 is not < 20.6 -> stops heating, returns to Dwell
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(21.0));
        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task Cooling_TurnsOn_AboveTargetPlusHysteresis_ThenTurnsOff_BelowTargetMinusHysteresis()
    {
        var harness = new ThermostatServiceHarness();
        var setPoint = ThermostatFixtures.CreateSetPoint(RunType.Cooling, targetTemperatureC: 24f);
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(RunMode.Cooling, setPoints: [setPoint])
        );

        await harness.StartAsync();

        // 24 + 0.6 = 24.6; 25.0 >= 24.6 -> starts cooling from Dwell
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(25.0));
        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Cooling),
        ]);
        harness.WrittenMessages.Clear();

        // 24 - 0.6 = 23.4; 23.0 is not > 23.4 -> stops cooling, returns to Dwell
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(23.0));
        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task SimultaneousHeatingAndCoolingCalls_WritesEnableThenDisable_NotDwell()
    {
        var harness = new ThermostatServiceHarness();
        // Deliberately inverted/contrived configuration: heating target above cooling target so both conditions can
        // be satisfied simultaneously from Dwell, triggering the defensive "both calls active" fallback.
        var heatingSetPoint = ThermostatFixtures.CreateSetPoint(
            RunType.Heating,
            targetTemperatureC: 25f
        );
        var coolingSetPoint = ThermostatFixtures.CreateSetPoint(
            RunType.Cooling,
            targetTemperatureC: 15f
        );
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(
                RunMode.Automatic,
                setPoints: [heatingSetPoint, coolingSetPoint]
            )
        );

        await harness.StartAsync();

        // 20 <= 25-0.6=24.4 (calls for heating) AND 20 >= 15+0.6=15.6 (calls for cooling) at the same time.
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(20.0));

        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Disable),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task ScheduleOverridesSetPoint_WhenActive()
    {
        var harness = new ThermostatServiceHarness();
        // setpoint alone would not call for heat at env=20 (target 10 -> needs <= 9.4), but the active schedule's
        // higher target (25 -> needs <= 24.4) does, proving the schedule value is the one used.
        var setPoint = ThermostatFixtures.CreateSetPoint(RunType.Heating, targetTemperatureC: 10f);
        var schedule = ThermostatFixtures.CreateActiveSchedule(
            RunType.Heating,
            targetTemperatureC: 25f
        );
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(
                RunMode.Heating,
                setPoints: [setPoint],
                schedules: [schedule]
            )
        );

        await harness.StartAsync();
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(20.0));

        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Heating),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task DisabledSchedule_IsIgnoredEvenWhenInWindow_FallsBackToSetPoint()
    {
        var harness = new ThermostatServiceHarness();
        var setPoint = ThermostatFixtures.CreateSetPoint(RunType.Heating, targetTemperatureC: 10f);
        var schedule = ThermostatFixtures.CreateActiveSchedule(
            RunType.Heating,
            targetTemperatureC: 25f,
            enabled: false
        );
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(
                RunMode.Heating,
                setPoints: [setPoint],
                schedules: [schedule]
            )
        );

        await harness.StartAsync();
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(20.0));

        // falls back to the setpoint's target of 10, which env=20 does not satisfy (needs <= 9.4)
        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task ScheduleOutsideTimeWindow_IsIgnored_FallsBackToSetPoint()
    {
        var harness = new ThermostatServiceHarness();
        var setPoint = ThermostatFixtures.CreateSetPoint(RunType.Heating, targetTemperatureC: 10f);
        var schedule = ThermostatFixtures.CreateInactiveSchedule(
            RunType.Heating,
            targetTemperatureC: 25f
        );
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(
                RunMode.Heating,
                setPoints: [setPoint],
                schedules: [schedule]
            )
        );

        await harness.StartAsync();
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(20.0));

        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task ForecastGating_Heating_BlocksCall_WhenForecastAboveActivation()
    {
        var harness = new ThermostatServiceHarness();
        var setPoint = ThermostatFixtures.CreateSetPoint(
            RunType.Heating,
            targetTemperatureC: 20f,
            activationTemperatureC: 5f
        );
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(RunMode.Heating, setPoints: [setPoint])
        );
        harness.SetWeatherForecast(10); // above activation of 5 -> forecast does not call for heating

        await harness.StartAsync();
        // env alone would call for heat (15 <= 19.4) but forecast gating blocks it
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(15.0));

        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task ForecastGating_Heating_AllowsCall_WhenForecastAtOrBelowActivation()
    {
        var harness = new ThermostatServiceHarness();
        var setPoint = ThermostatFixtures.CreateSetPoint(
            RunType.Heating,
            targetTemperatureC: 20f,
            activationTemperatureC: 5f
        );
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(RunMode.Heating, setPoints: [setPoint])
        );
        harness.SetWeatherForecast(3); // at or below activation of 5 -> forecast calls for heating

        await harness.StartAsync();
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(15.0));

        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Heating),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task ForecastGating_Cooling_BlocksCall_WhenForecastBelowActivation()
    {
        var harness = new ThermostatServiceHarness();
        var setPoint = ThermostatFixtures.CreateSetPoint(
            RunType.Cooling,
            targetTemperatureC: 20f,
            activationTemperatureC: 25f
        );
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(RunMode.Cooling, setPoints: [setPoint])
        );
        harness.SetWeatherForecast(20); // below activation of 25 -> forecast does not call for cooling

        await harness.StartAsync();
        // env alone would call for cooling (25 >= 20.6) but forecast gating blocks it
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(25.0));

        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task ForecastGating_Cooling_AllowsCall_WhenForecastAtOrAboveActivation()
    {
        var harness = new ThermostatServiceHarness();
        var setPoint = ThermostatFixtures.CreateSetPoint(
            RunType.Cooling,
            targetTemperatureC: 20f,
            activationTemperatureC: 25f
        );
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(RunMode.Cooling, setPoints: [setPoint])
        );
        harness.SetWeatherForecast(30); // at or above activation of 25 -> forecast calls for cooling

        await harness.StartAsync();
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(25.0));

        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Cooling),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task WeatherDispatchFailure_LocationNotConfigured_FallsBackToEnvironmentOnlyLogic()
    {
        var harness = new ThermostatServiceHarness();
        // ActivationTemperatureC is configured, but since there's no location, forecastTemperatureC stays null, so
        // useForecastForHeating must be false and the call decision falls back to env-only logic.
        var setPoint = ThermostatFixtures.CreateSetPoint(
            RunType.Heating,
            targetTemperatureC: 20f,
            activationTemperatureC: 5f
        );
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(RunMode.Heating, setPoints: [setPoint])
        );
        harness.SetWeatherFailure(GetCurrentLocationErrors.LocationNotConfigured);

        await harness.StartAsync();
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(19.0));

        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Heating),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task WeatherDispatchFailure_OtherError_AlsoFallsBackToEnvironmentOnlyLogic()
    {
        var harness = new ThermostatServiceHarness();
        var setPoint = ThermostatFixtures.CreateSetPoint(
            RunType.Heating,
            targetTemperatureC: 20f,
            activationTemperatureC: 5f
        );
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(RunMode.Heating, setPoints: [setPoint])
        );
        harness.SetWeatherFailure(GetWeatherForecastErrors.ForecastNotFound);

        await harness.StartAsync();
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(19.0));

        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Heating),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task ModeGating_CallForHeating_IsIgnored_WhenModeIsCoolingNotAutomatic()
    {
        var harness = new ThermostatServiceHarness();
        var setPoint = ThermostatFixtures.CreateSetPoint(RunType.Heating, targetTemperatureC: 20f);
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(RunMode.Cooling, setPoints: [setPoint])
        );

        await harness.StartAsync();
        // would call for heat (19 <= 19.4) but Mode is Cooling, not Heating/Automatic
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(19.0));

        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task ModeGating_CallForCooling_IsIgnored_WhenModeIsHeatingNotAutomatic()
    {
        var harness = new ThermostatServiceHarness();
        var setPoint = ThermostatFixtures.CreateSetPoint(RunType.Cooling, targetTemperatureC: 24f);
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(RunMode.Heating, setPoints: [setPoint])
        );

        await harness.StartAsync();
        // would call for cooling (25 >= 24.6) but Mode is Heating, not Cooling/Automatic
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(25.0));

        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task Hysteresis_TooLow_FallsBackToDefault()
    {
        var harness = new ThermostatServiceHarness();
        var setPoint = ThermostatFixtures.CreateSetPoint(RunType.Heating, targetTemperatureC: 20f);
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(
                RunMode.Heating,
                hysteresisC: 0.1f,
                setPoints: [setPoint]
            )
        );

        await harness.StartAsync();
        // If the invalid 0.1 were honored the start threshold would be 20 - 0.1 = 19.9 and 19.5 would call for heat.
        // With the 0.6 default the threshold is 19.4, so 19.5 must NOT call for heat.
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(19.5));

        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task Hysteresis_TooHigh_FallsBackToDefault()
    {
        var harness = new ThermostatServiceHarness();
        var setPoint = ThermostatFixtures.CreateSetPoint(RunType.Heating, targetTemperatureC: 20f);
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(
                RunMode.Heating,
                hysteresisC: 3.0f,
                setPoints: [setPoint]
            )
        );

        await harness.StartAsync();
        // If the invalid 3.0 were honored the start threshold would be 20 - 3.0 = 17.0 and 19.0 would not call for
        // heat. With the 0.6 default the threshold is 19.4, so asserting Heating proves the default was used.
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(19.0));

        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Heating),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task Hysteresis_ValidCustomValue_IsHonored()
    {
        var harness = new ThermostatServiceHarness();
        var setPoint = ThermostatFixtures.CreateSetPoint(RunType.Heating, targetTemperatureC: 20f);
        // valid custom hysteresis of 1.0 -> threshold is 20 - 1.0 = 19.0
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(
                RunMode.Heating,
                hysteresisC: 1.0f,
                setPoints: [setPoint]
            )
        );

        await harness.StartAsync();
        // With the custom 1.0 the start threshold is 20 - 1.0 = 19.0, so 19.3 must NOT call for heat; with the 0.6
        // default the threshold would be 19.4 and 19.3 would call for heat. Dwell proves the custom value was used.
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(19.3));

        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task Enable_IsReassertedEveryCycle_NotJustOnFirstIteration()
    {
        var harness = new ThermostatServiceHarness();
        var setPoint = ThermostatFixtures.CreateSetPoint(RunType.Heating, targetTemperatureC: 20f);
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(RunMode.Heating, setPoints: [setPoint])
        );

        await harness.StartAsync();

        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(19.0));
        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Heating),
        ]);
        harness.WrittenMessages.Clear();

        // still within the "keep heating" band (< target+hysteresis = 20.6), remains Heating on the second iteration
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(19.5));
        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Heating),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task Automatic_Mode_CyclesHeatingAndCooling_AlwaysPassingThroughDwell()
    {
        var harness = new ThermostatServiceHarness();
        var heatingSetPoint = ThermostatFixtures.CreateSetPoint(
            RunType.Heating,
            targetTemperatureC: 20f
        );
        var coolingSetPoint = ThermostatFixtures.CreateSetPoint(
            RunType.Cooling,
            targetTemperatureC: 24f
        );
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(
                RunMode.Automatic,
                setPoints: [heatingSetPoint, coolingSetPoint]
            )
        );

        await harness.StartAsync();

        // 19.0 <= 20-0.6 -> heating call is honored in Automatic mode
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(19.0));
        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Heating),
        ]);
        harness.WrittenMessages.Clear();

        // 21.0 is not < 20+0.6 -> heating stops, back to Dwell
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(21.0));
        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);
        harness.WrittenMessages.Clear();

        // 25.0 >= 24+0.6 -> cooling call is honored in Automatic mode
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(25.0));
        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Cooling),
        ]);
        harness.WrittenMessages.Clear();

        // 23.0 is not > 24-0.6 -> cooling stops, back to Dwell
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(23.0));
        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task Heating_StartThresholdIsInclusive_AndStopThresholdIsExclusive()
    {
        var harness = new ThermostatServiceHarness();
        var setPoint = ThermostatFixtures.CreateSetPoint(RunType.Heating, targetTemperatureC: 20f);
        // 0.5 is inside the safe range and, unlike 0.6, is exactly representable in binary floating point, so the
        // boundary values below compare exactly rather than being at the mercy of float/double rounding.
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(
                RunMode.Heating,
                hysteresisC: 0.5f,
                setPoints: [setPoint]
            )
        );

        await harness.StartAsync();

        // start threshold is inclusive: 19.5 <= 20 - 0.5
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(19.5));
        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Heating),
        ]);
        harness.WrittenMessages.Clear();

        // stop threshold is exclusive: 20.5 < 20 + 0.5 is false, so the call ends exactly at the boundary
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(20.5));
        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task ForecastGating_Heating_StopsAnActiveCall_WhenForecastRisesAboveActivation()
    {
        var harness = new ThermostatServiceHarness();
        var setPoint = ThermostatFixtures.CreateSetPoint(
            RunType.Heating,
            targetTemperatureC: 20f,
            activationTemperatureC: 5f
        );
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(RunMode.Heating, setPoints: [setPoint])
        );
        harness.SetWeatherForecast(3);

        await harness.StartAsync();

        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(19.0));
        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Heating),
        ]);
        harness.WrittenMessages.Clear();

        // 19.5 is still inside the "keep heating" band (< 20.6), but the forecast no longer calls for heat, so the
        // active call must be dropped rather than continued.
        harness.SetWeatherForecast(10);
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(19.5));
        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task WeatherSuccessWithoutCurrentReading_FallsBackToEnvironmentOnlyLogic()
    {
        var harness = new ThermostatServiceHarness();
        var setPoint = ThermostatFixtures.CreateSetPoint(
            RunType.Heating,
            targetTemperatureC: 20f,
            activationTemperatureC: 5f
        );
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(RunMode.Heating, setPoints: [setPoint])
        );
        // the forecast lookup succeeds but carries no current reading, so forecast gating must be skipped entirely
        harness.SetWeatherForecast(null);

        await harness.StartAsync();
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(19.0));

        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Heating),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task MidnightSpanningSchedule_IsTreatedAsActive()
    {
        var harness = new ThermostatServiceHarness();
        // the setpoint alone would not call for heat at env=20 (target 10 -> needs <= 9.4); only the wrap-around
        // schedule's target of 25 does, proving the midnight-spanning window was evaluated as active.
        var setPoint = ThermostatFixtures.CreateSetPoint(RunType.Heating, targetTemperatureC: 10f);
        var schedule = ThermostatFixtures.CreateMidnightSpanningActiveSchedule(
            RunType.Heating,
            targetTemperatureC: 25f
        );
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(
                RunMode.Heating,
                setPoints: [setPoint],
                schedules: [schedule]
            )
        );

        await harness.StartAsync();
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(20.0));

        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Heating),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task SwitchingToOff_ResetsActiveCall_SoHeatingDoesNotResumeInsideTheContinuationBand()
    {
        var harness = new ThermostatServiceHarness();
        var setPoint = ThermostatFixtures.CreateSetPoint(RunType.Heating, targetTemperatureC: 20f);
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(RunMode.Heating, setPoints: [setPoint])
        );

        await harness.StartAsync();

        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(19.0));
        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Heating),
        ]);
        harness.WrittenMessages.Clear();

        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(RunMode.Off, setPoints: [setPoint])
        );
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(19.5));
        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);
        harness.WrittenMessages.Clear();

        // 19.5 sits inside the "keep heating" band (< 20.6) but above the start threshold (19.4); because the call
        // was reset to Dwell while the thermostat was Off, heating must not resume until 19.4 is crossed again.
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(RunMode.Heating, setPoints: [setPoint])
        );
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(19.5));
        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);

        await harness.StopAsync();
    }

    [Fact]
    public async Task SwitchingToDisabled_ResetsActiveCall_SoHeatingDoesNotResumeInsideTheContinuationBand()
    {
        var harness = new ThermostatServiceHarness();
        var setPoint = ThermostatFixtures.CreateSetPoint(RunType.Heating, targetTemperatureC: 20f);
        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(RunMode.Heating, setPoints: [setPoint])
        );

        await harness.StartAsync();

        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(19.0));
        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Heating),
        ]);
        harness.WrittenMessages.Clear();

        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(RunMode.Disabled, setPoints: [setPoint])
        );
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(19.5));
        harness.WrittenMessages.ShouldBe([new ControlMessage(ControlState.Disable)]);
        harness.WrittenMessages.Clear();

        harness.SetThermostat(
            ThermostatFixtures.CreateThermostat(RunMode.Heating, setPoints: [setPoint])
        );
        await harness.PushEnvironmentAsync(ThermostatFixtures.CreateEnvironment(19.5));
        harness.WrittenMessages.ShouldBe([
            new ControlMessage(ControlState.Enable),
            new ControlMessage(ControlState.Dwell),
        ]);

        await harness.StopAsync();
    }
}
