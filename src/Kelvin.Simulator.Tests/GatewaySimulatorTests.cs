using System.Globalization;
using Shouldly;
using Xunit;

namespace Kelvin.Simulator.Tests;

public class GatewaySimulatorTests
{
    [Theory]
    [InlineData("Heating", "Cooling", "Warming", 24.8f)]
    [InlineData("Cooling", "Heating", "Cooling", 23.2f)]
    [InlineData("Dwell", "Heating", "Cooling", 23.2f)]
    [InlineData("Dwell", "Cooling", "Warming", 24.8f)]
    [InlineData("Dwell", "Off", "Neutral", 24f)]
    [InlineData("Dwell", "Disabled", "Neutral", 24f)]
    [InlineData("Dwell", "Automatic", "Neutral", 24f)]
    public void Auto_ResolvesCallBeforeMode(string call, string mode, string trend, float target)
    {
        var status = new GatewayStatus(
            new ThermostatSnapshot(mode, false, 0.4f),
            new ControlStateSnapshot(
                "Enable",
                call,
                false,
                new ControlStateChangeSnapshot(99f, 9f)
            ),
            new ControlStateChangeSnapshot(24f, 0.8f)
        );

        Resolve(SimulatorScenario.Auto, status).ShouldBe((trend, target));
    }

    [Theory]
    [InlineData("Heating", "Cooling", 20.9f)]
    [InlineData("Cooling", "Warming", 22.1f)]
    [InlineData("Off", "Neutral", 21.5f)]
    public void Auto_MissingCallContext_UsesBaseTemperatureAndThermostatHysteresis(
        string mode,
        string trend,
        float target
    )
    {
        Resolve(
                SimulatorScenario.Auto,
                new GatewayStatus(new ThermostatSnapshot(mode, false, 0.6f))
            )
            .ShouldBe((trend, target));
    }

    [Fact]
    public void Auto_MissingThermostat_UsesDefaultHysteresis()
    {
        var status = new GatewayStatus(
            Control: new ControlStateSnapshot("Enable", "Heating", false, null)
        );

        Resolve(SimulatorScenario.Auto, status).ShouldBe(("Warming", 22.1f));
        Resolve(SimulatorScenario.Auto, new GatewayStatus()).ShouldBe(("Neutral", 21.5f));
    }

    [Theory]
    [InlineData("Heating", "Warming", 23f)]
    [InlineData("Cooling", "Cooling", 20f)]
    [InlineData("Idle", "Neutral", 21.5f)]
    public void ForcedScenario_IgnoresServerState(string scenario, string trend, float target)
    {
        var status = new GatewayStatus(CallContext: new ControlStateChangeSnapshot(99f, 9f));

        Resolve(Enum.Parse<SimulatorScenario>(scenario), status).ShouldBe((trend, target));
    }

    [Theory]
    [InlineData(30f, 30, 21.75f)]
    [InlineData(10f, 30, 21.25f)]
    [InlineData(21.6f, 30, 21.6f)]
    [InlineData(21.4f, 30, 21.4f)]
    [InlineData(30f, 0, 21.5f)]
    [InlineData(30f, -30, 21.5f)]
    public void Ambient_MovesAtHalfADegreePerMinuteWithoutOvershooting(
        float target,
        int seconds,
        float expected
    )
    {
        var ambient = new AmbientSimulation(21.5f);

        ambient.Step(
            new AmbientDirective(AmbientTrend.Neutral, target),
            0f,
            TimeSpan.FromSeconds(seconds)
        );

        ambient.TemperatureC.ShouldBe(expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApplyScenario_CorrectsForActiveSensorAndHmiOffsets(bool disableSomeDevices)
    {
        var gateway = CreateGateway(
            TimeSpan.FromHours(1),
            out var sensors,
            out var hmis,
            out var ambient
        );
        if (disableSomeDevices)
        {
            sensors.SetSensorEnabled(0, false, out _);
            hmis.SetHmiEnabled(0, false, out _);
        }

        gateway.ApplyScenario();

        var readings = sensors
            .ActiveSensors.Select(sensor => sensor.TemperatureC)
            .Concat(hmis.ActiveHmis.Select(hmi => hmi.TemperatureC));
        readings.Average().ShouldBe(21.5f, 0.0001f);
        ambient.LastDirective.TargetTemperatureC.ShouldBe(21.5f);
    }

    [Fact]
    public void ApplyScenario_NoActiveDevices_HasZeroBias()
    {
        var gateway = CreateGateway(
            TimeSpan.FromHours(1),
            out var sensors,
            out var hmis,
            out var ambient
        );
        sensors.SetAllSensorsEnabled(false);
        hmis.SetAllHmisEnabled(false);

        gateway.ApplyScenario();

        ambient.TemperatureC.ShouldBe(21.5f);
    }

    public static IEnumerable<object[]> Commands()
    {
        yield return ["", new StatusCommand()];
        yield return ["unknown", new StatusCommand()];
        yield return ["base 24.5", new SetBaseTempCommand(24.5f)];
        yield return ["base invalid", new StatusCommand()];
        yield return ["BASE HUMIDITY 82.5", new SetBaseHumidityCommand(82.5f)];
        yield return ["base humidity 0", new SetBaseHumidityCommand(0f)];
        yield return ["base humidity 100", new SetBaseHumidityCommand(100f)];
        yield return ["base humidity", new InvalidBaselineCommand()];
        yield return ["base humidity -1", new InvalidBaselineCommand()];
        yield return ["base humidity 101", new InvalidBaselineCommand()];
        yield return ["base humidity NaN", new InvalidBaselineCommand()];
        yield return ["base humidity 45 extra", new InvalidBaselineCommand()];
        yield return ["base co2 3500", new SetBaseCo2Command(3500)];
        yield return ["base co2 0", new SetBaseCo2Command(0)];
        yield return ["base co2 65535", new SetBaseCo2Command(65535)];
        yield return ["base co2 -1", new InvalidBaselineCommand()];
        yield return ["base co2 65536", new InvalidBaselineCommand()];
        yield return ["base co2 500.5", new InvalidBaselineCommand()];
        yield return ["base co2 500 extra", new InvalidBaselineCommand()];
        yield return ["add", new AddSensorCommand()];
        yield return ["remove 2", new RemoveSensorCommand(2)];
        yield return ["enable 1", new ToggleSensorCommand(1, true)];
        yield return ["disable all", new ToggleAllSensorsCommand(false)];
        yield return ["scenario heating", new SetScenarioCommand(SimulatorScenario.Heating)];
        yield return ["scenario invalid", new StatusCommand()];
        yield return ["humidifier add", new AddHumidifierCommand()];
        yield return ["humidifier remove", new RemoveHumidifierCommand()];
        yield return ["hmi", new ListHmisCommand()];
        yield return ["hmi add", new AddHmiCommand()];
        yield return ["hmi remove 2", new RemoveHmiCommand(2)];
        yield return ["hmi enable all", new ToggleAllHmisCommand(true)];
        yield return ["hmi disable 1", new ToggleHmiCommand(1, false)];
        yield return ["hmi remove", new HmiHelpCommand()];
        yield return ["hmi state", new HmiStateCommand(0)];
        yield return ["hmi 2 reading", new HmiSendReadingCommand(2)];
        yield return ["  HMI 2 MODE cooling  ", new HmiSetModeCommand(2, HmiRunMode.Cooling)];
        yield return ["hmi mode 2", new HmiHelpCommand()];
        yield return ["hmi mode invalid", new HmiHelpCommand()];
        yield return ["hmi fan on", new HmiSetFanCommand(0, true)];
        yield return ["hmi fan invalid", new HmiHelpCommand()];
        yield return
        [
            "hmi setpoint heating 23.5",
            new HmiSetSetPointCommand(0, HmiRunType.Heating, 23.5f),
        ];
        yield return
        [
            "hmi schedule add heating 08:00 17:00 22",
            new HmiUpsertScheduleCommand(
                0,
                null,
                HmiRunType.Heating,
                new TimeOnly(8, 0),
                new TimeOnly(17, 0),
                22f
            ),
        ];
        yield return
        [
            "hmi 2 schedule update 1 cooling 08:00 17:00 24",
            new HmiUpsertScheduleCommand(
                2,
                1,
                HmiRunType.Cooling,
                new TimeOnly(8, 0),
                new TimeOnly(17, 0),
                24f
            ),
        ];
        yield return ["hmi schedule remove 1", new HmiRemoveScheduleCommand(0, 1)];
        yield return ["hmi schedule add heating invalid 17:00 22", new HmiHelpCommand()];
        yield return ["hmi lockouts none 26", new HmiSetLockoutsCommand(0, null, 26f)];
        yield return ["hmi lockouts 10 none", new HmiSetLockoutsCommand(0, 10f, null)];
        yield return ["hmi lockouts invalid none", new HmiHelpCommand()];
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public void ParseCommand_PreservesCommandArguments(string input, object expected)
    {
        SimulatorCommandParser.Parse(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(85f, 3500)]
    [InlineData(100f, 65535)]
    public void BaselineCommands_UpdateExistingAndNewDevicesAndPersistAcrossTicks(
        float humidity,
        int co2
    )
    {
        var gateway = CreateGateway(TimeSpan.FromSeconds(30), out var sensors, out var hmis, out _);
        var controller = new HmiController(hmis, _ => { }, _ => { }, (_, _) => { });
        sensors.SetSensorEnabled(0, false, out var offlineSensor).ShouldBeTrue();
        hmis.SetHmiEnabled(0, false, out var offlineHmi).ShouldBeTrue();

        gateway.ExecuteCommand(
            SimulatorCommandParser.Parse($"base humidity {humidity.ToString(CultureInfo.InvariantCulture)}"),
            controller
        );
        gateway.ExecuteCommand(SimulatorCommandParser.Parse($"base co2 {co2}"), controller);
        offlineSensor.ShouldNotBeNull().HumidityPercentage.ShouldBe(humidity);
        offlineSensor.CO2LevelPpm.ShouldBe((ushort)co2);
        offlineHmi.ShouldNotBeNull().HumidityPercentage.ShouldBe(humidity);

        gateway.ExecuteCommand(new AddSensorCommand(), controller);
        gateway.ExecuteCommand(new AddHmiCommand(), controller);
        var addedSensor = sensors.Sensors[^1];
        hmis.TryGet(hmis.Count - 1, out var addedHmi).ShouldBeTrue();
        addedSensor.HumidityPercentage.ShouldBe(humidity);
        addedSensor.CO2LevelPpm.ShouldBe((ushort)co2);
        addedHmi.ShouldNotBeNull().HumidityPercentage.ShouldBe(humidity);

        for (var i = 0; i < 30; i++)
        {
            gateway.ApplyScenario();
            foreach (var sensor in sensors.Sensors)
            {
                sensor.HumidityPercentage.ShouldBeInRange(Math.Max(0f, humidity - 0.15f), Math.Min(100f, humidity + 0.15f));
                ((int)sensor.CO2LevelPpm).ShouldBeInRange(Math.Max(0, co2 - 5), Math.Min(65535, co2 + 5));
            }
            offlineHmi.HumidityPercentage.ShouldBeInRange(Math.Max(0f, humidity - 0.15f), Math.Min(100f, humidity + 0.15f));
            addedHmi.HumidityPercentage.ShouldBeInRange(Math.Max(0f, humidity - 0.15f), Math.Min(100f, humidity + 0.15f));
        }
    }

    private static (string Trend, float Target) Resolve(
        SimulatorScenario scenario,
        GatewayStatus status
    )
    {
        var ambient = new AmbientSimulation(21.5f) { Scenario = scenario };
        var directive = ambient.ResolveDirective(status);
        return (directive.Trend.ToString(), directive.TargetTemperatureC);
    }

    private static GatewaySimulator CreateGateway(
        TimeSpan interval,
        out SensorFleet sensors,
        out HmiFleet hmis,
        out AmbientSimulation ambient
    )
    {
        var options = SimulatorOptions.Parse(["--port=COM5"]) with { Interval = interval };
        sensors = new SensorFleet(options.SensorCount, options.BaseTemperatureC);
        hmis = new HmiFleet(options.HmiCount, options.BaseTemperatureC);
        ambient = new AmbientSimulation(options.BaseTemperatureC);
        return new GatewaySimulator(options, sensors, hmis, ambient);
    }
}
