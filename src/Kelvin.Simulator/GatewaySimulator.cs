using System.Globalization;
using System.Threading.Channels;

namespace Kelvin.Simulator;

internal sealed class GatewaySimulator(
    SimulatorOptions options,
    SensorFleet sensors,
    HmiFleet hmis,
    AmbientSimulation ambient
)
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
    private readonly Channel<SimulatorCommand> commandChannel =
        Channel.CreateUnbounded<SimulatorCommand>();
    private readonly SemaphoreSlim gate = new(1, 1);
    private SimulatedHumidifier? humidifier = options.HumidifierEnabled
        ? new SimulatedHumidifier()
        : null;
    private GatewayStatus gatewayStatus = new();
    private string? lastLoggedCallState;
    private string? lastLoggedMode;
    private bool? lastLoggedFanOn;
    private AmbientTrend? lastLoggedTrend;
    private float? lastLoggedTargetC;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var connection = new GatewayConnection(options.PortName);
        using var httpClient = new HttpClient();
        var serverClient = new KelvinServerClient(options.ServerUrl, httpClient, LogDebug);
        var controller = new HmiController(hmis, connection.Write, Console.WriteLine, LogDebug);
        connection.Open();
        Console.WriteLine(
            $"Kelvin simulator connected to {options.PortName} with {sensors.Count} sensor(s), {hmis.Count} HMI(s), and {(humidifier is null ? 0 : 1)} humidifier(s)."
        );
        Console.WriteLine($"Kelvin.Server target: {options.ServerUrl}");
        Console.WriteLine("Press Ctrl+C to stop.");
        Console.WriteLine(
            "Commands: base <temp>, add, remove <index>, enable <index|all>, disable <index|all>, scenario <auto|idle|heating|cooling>, list, status, hmi (type 'hmi help'), humidifier <add|remove>"
        );

        var commandTask = options.Interactive
            ? Task.Run(() => ReadCommandsAsync(cancellationToken), cancellationToken)
            : Task.CompletedTask;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ProcessCommandsAsync(controller);
                gatewayStatus = await serverClient.RefreshAsync(gatewayStatus, cancellationToken);
                TickHumidifier(connection);
                ReadIncoming(connection, controller);
                ApplyScenario();

                foreach (var sensor in sensors.ActiveSensors)
                {
                    connection.Write(GatewayProtocol.EncodeSensorReading(sensor));
                    Console.WriteLine(sensor);
                }
                foreach (var hmi in hmis.ActiveHmis)
                    controller.WriteReading(hmi);

                await WaitForNextTickAsync(connection, controller, cancellationToken);
            }
            catch (TimeoutException) { }
        }

        await commandTask;
    }

    private async Task ReadCommandsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await Console.In.ReadLineAsync();
            if (line is null)
                return;
            await commandChannel.Writer.WriteAsync(
                SimulatorCommandParser.Parse(line),
                cancellationToken
            );
        }
    }

    private async Task WaitForNextTickAsync(
        GatewayConnection connection,
        HmiController controller,
        CancellationToken cancellationToken
    )
    {
        var deadline = DateTime.UtcNow + options.Interval;
        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            await Task.Delay(
                remaining < PollInterval ? remaining : PollInterval,
                cancellationToken
            );
            ReadIncoming(connection, controller);
            await ProcessCommandsAsync(controller);
            TickHumidifier(connection);
        }
    }

    private async Task ProcessCommandsAsync(HmiController controller)
    {
        while (commandChannel.Reader.TryRead(out var command))
        {
            await gate.WaitAsync();
            try
            {
                switch (command)
                {
                    case SetBaseTempCommand temperature:
                        ambient.SetBaseTemperature(temperature.TemperatureC);
                        Console.WriteLine(
                            $"Base temperature set to {temperature.TemperatureC.ToString("F1", CultureInfo.InvariantCulture)}C."
                        );
                        break;
                    case AddSensorCommand:
                        var sensor = sensors.AddSensor(ambient.BaseTemperatureC);
                        Console.WriteLine(
                            $"Sensor added. Total sensors: {sensors.Count}. Added {sensor}."
                        );
                        break;
                    case RemoveSensorCommand removeSensor:
                        if (sensors.RemoveSensor(removeSensor.Index, out var removedSensor))
                            Console.WriteLine(
                                $"Sensor {removeSensor.Index} removed. Total sensors: {sensors.Count}. Removed {removedSensor}."
                            );
                        else
                            Console.WriteLine("Invalid sensor index.");
                        break;
                    case ToggleSensorCommand toggleSensor:
                        if (
                            sensors.SetSensorEnabled(
                                toggleSensor.Index,
                                toggleSensor.Enabled,
                                out var toggledSensor
                            )
                        )
                            Console.WriteLine(
                                $"Sensor {toggleSensor.Index} {(toggleSensor.Enabled ? "enabled" : "disabled")}. {toggledSensor}"
                            );
                        else
                            Console.WriteLine("Invalid sensor index.");
                        break;
                    case ToggleAllSensorsCommand toggleSensors:
                        var sensorCount = sensors.SetAllSensorsEnabled(toggleSensors.Enabled);
                        Console.WriteLine(
                            $"{sensorCount} sensor(s) {(toggleSensors.Enabled ? "enabled" : "disabled")}. "
                        );
                        break;
                    case SetScenarioCommand setScenario:
                        ambient.Scenario = setScenario.Scenario;
                        Console.WriteLine($"Scenario set to {ambient.Scenario}.");
                        break;
                    case ListSensorsCommand:
                        for (var index = 0; index < sensors.Count; index++)
                            Console.WriteLine(sensors.Describe(index));
                        break;
                    case StatusCommand:
                        PrintStatus();
                        break;
                    case AddHumidifierCommand:
                        humidifier ??= new SimulatedHumidifier();
                        Console.WriteLine("Humidifier added.");
                        break;
                    case RemoveHumidifierCommand:
                        humidifier = null;
                        Console.WriteLine("Humidifier removed.");
                        break;
                    case AddHmiCommand:
                        var hmi = hmis.AddHmi(ambient.BaseTemperatureC);
                        Console.WriteLine($"HMI added. Total HMIs: {hmis.Count}. Added {hmi}.");
                        break;
                    case RemoveHmiCommand removeHmi:
                        if (hmis.RemoveHmi(removeHmi.Index, out var removedHmi))
                            Console.WriteLine(
                                $"HMI {removeHmi.Index} removed. Total HMIs: {hmis.Count}. Removed {removedHmi}."
                            );
                        else
                            Console.WriteLine("Invalid HMI index.");
                        break;
                    case ToggleHmiCommand toggleHmi:
                        if (
                            hmis.SetHmiEnabled(
                                toggleHmi.Index,
                                toggleHmi.Enabled,
                                out var toggledHmi
                            )
                        )
                            Console.WriteLine(
                                $"HMI {toggleHmi.Index} {(toggleHmi.Enabled ? "enabled" : "disabled")}. {toggledHmi}"
                            );
                        else
                            Console.WriteLine("Invalid HMI index.");
                        break;
                    case ToggleAllHmisCommand toggleHmis:
                        var hmiCount = hmis.SetAllHmisEnabled(toggleHmis.Enabled);
                        Console.WriteLine(
                            $"{hmiCount} HMI(s) {(toggleHmis.Enabled ? "enabled" : "disabled")}. "
                        );
                        break;
                    case ListHmisCommand:
                        for (var index = 0; index < hmis.Count; index++)
                            Console.WriteLine(hmis.Describe(index));
                        break;
                    case HmiHelpCommand:
                        controller.PrintHelp();
                        break;
                    case HmiDeviceCommand deviceCommand:
                        controller.Execute(deviceCommand);
                        break;
                }
            }
            finally
            {
                gate.Release();
            }
        }
    }

    internal void ApplyScenario()
    {
        var directive = ambient.ResolveDirective(gatewayStatus);
        LogDirective(directive);
        ambient.Step(directive, FleetBiasC, options.Interval);
        sensors.StepAll(ambient.TemperatureC);
        hmis.StepAll(ambient.TemperatureC);
    }

    private float FleetBiasC
    {
        get
        {
            var offsets = sensors
                .ActiveSensors.Select(sensor => sensor.RoomOffsetC)
                .Concat(hmis.ActiveHmis.Select(hmi => hmi.RoomOffsetC))
                .ToList();
            return offsets.Count == 0 ? 0f : offsets.Average();
        }
    }

    private void ReadIncoming(GatewayConnection connection, HmiController controller) =>
        connection.ReadIncoming((mac, payload) => HandleDownlink(controller, mac, payload));

    private void HandleDownlink(HmiController controller, byte[] macAddress, byte[] payload)
    {
        if (macAddress.AsSpan().SequenceEqual(SimulatedHumidifier.MacAddress))
        {
            if (humidifier?.ReceiveState(payload, DateTimeOffset.UtcNow) == true)
                Console.WriteLine($"[humidifier] <- State relayOn={humidifier.RelayOn}");
            else
                LogDebug(
                    DebugLevel.Info,
                    "[humidifier] absent, unpowered, or invalid state; dropped reply."
                );
            return;
        }
        controller.Receive(macAddress, payload);
    }

    private void TickHumidifier(GatewayConnection connection)
    {
        var payload = humidifier?.Tick(gatewayStatus.Control, DateTimeOffset.UtcNow);
        if (payload is null)
            return;
        connection.Write(GatewayProtocol.EncodeUplink(SimulatedHumidifier.MacAddress, payload));
        Console.WriteLine($"[humidifier] -> Ping relayOn={humidifier!.RelayOn}");
    }

    private void LogDebug(DebugLevel level, string message)
    {
        if (options.Debug >= level)
            Console.WriteLine($"[debug] {message}");
    }

    private void LogDirective(AmbientDirective directive)
    {
        var callState = gatewayStatus.Control?.CallState;
        var mode = gatewayStatus.Thermostat?.Mode;
        var fanOn = gatewayStatus.Control?.FanOn;
        var changed =
            callState != lastLoggedCallState
            || mode != lastLoggedMode
            || fanOn != lastLoggedFanOn
            || directive.Trend != lastLoggedTrend
            || directive.TargetTemperatureC != lastLoggedTargetC;

        LogDebug(
            changed ? DebugLevel.Info : DebugLevel.Verbose,
            $"callState={callState ?? "unknown"} mode={mode ?? "unknown"} fanOn={fanOn} "
                + $"trend={directive.Trend} target={directive.TargetTemperatureC:F2}C ambient={ambient.TemperatureC:F2}C "
                + $"fleetBias={FleetBiasC:F2}C "
                + $"callTarget={gatewayStatus.CallContext?.TargetTemperatureC} callHysteresis={gatewayStatus.CallContext?.HysteresisC}"
        );

        if (changed)
        {
            lastLoggedCallState = callState;
            lastLoggedMode = mode;
            lastLoggedFanOn = fanOn;
            lastLoggedTrend = directive.Trend;
            lastLoggedTargetC = directive.TargetTemperatureC;
        }
    }

    private void PrintStatus()
    {
        Console.WriteLine($"Scenario: {ambient.Scenario}");
        Console.WriteLine($"Thermostat mode: {gatewayStatus.Thermostat?.Mode ?? "unknown"}");
        Console.WriteLine($"Control call: {gatewayStatus.Control?.CallState ?? "unknown"}");
        Console.WriteLine($"Sensors: {sensors.Count}");
        Console.WriteLine($"HMIs: {hmis.Count}");
        Console.WriteLine(
            humidifier is null
                ? "Humidifier: absent"
                : $"Humidifier: powered={humidifier.Powered} relayOn={humidifier.RelayOn}"
        );
        Console.WriteLine($"Ambient trend: {ambient.LastDirective.Trend}");
        Console.WriteLine(
            $"Ambient target: {ambient.LastDirective.TargetTemperatureC.ToString("F1", CultureInfo.InvariantCulture)}C"
        );
        Console.WriteLine(
            $"Base temperature: {ambient.BaseTemperatureC.ToString("F1", CultureInfo.InvariantCulture)}C"
        );
        Console.WriteLine(
            $"Ambient temperature: {ambient.TemperatureC.ToString("F1", CultureInfo.InvariantCulture)}C"
        );
        Console.WriteLine(
            $"Call target: {gatewayStatus.CallContext?.TargetTemperatureC?.ToString("F1", CultureInfo.InvariantCulture) ?? "unknown"}C"
        );
        Console.WriteLine(
            $"Call hysteresis: {gatewayStatus.CallContext?.HysteresisC?.ToString("F2", CultureInfo.InvariantCulture) ?? "unknown"}C"
        );
    }
}
