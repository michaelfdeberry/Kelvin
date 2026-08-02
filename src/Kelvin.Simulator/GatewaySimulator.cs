using System.Buffers.Binary;
using System.Globalization;
using System.IO.Ports;
using System.Net.Http.Json;
using System.Threading.Channels;

namespace Kelvin.Simulator;

internal sealed class GatewaySimulator
{
    private const byte PacketHeaderFirst = 0xAA;
    private const byte PacketHeaderSecond = 0x55;
    private const byte InfoHeaderFirst = 0xAB;
    private const byte InfoHeaderSecond = 0x56;
    private const int MacLength = 6;
    private const int PayloadLength = 16;
    private static readonly byte[] GatewayMac = [0x02, 0x11, 0x22, 0x33, 0x44, 0x55];
    private static readonly HttpClient HttpClient = new();

    private readonly SimulatorOptions options;
    private readonly SensorFleet sensors;
    private readonly Channel<SimulatorCommand> commandChannel =
        Channel.CreateUnbounded<SimulatorCommand>();
    private readonly SemaphoreSlim gate = new(1, 1);
    private SimulatorScenario scenario = SimulatorScenario.Auto;
    private GatewayStatus gatewayStatus = new();
    private float baseTemperatureC;

    public GatewaySimulator(SimulatorOptions options)
    {
        this.options = options;
        baseTemperatureC = options.BaseTemperatureC;
        sensors = new SensorFleet(options.SensorCount, baseTemperatureC);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var port = new SerialPort(options.PortName, 9600, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            NewLine = "\n",
            ReadTimeout = 250,
            WriteTimeout = 250,
        };

        port.Open();
        Console.WriteLine(
            $"Kelvin simulator connected to {options.PortName} with {sensors.Count} sensor(s)."
        );
        Console.WriteLine($"Kelvin.Server target: {options.ServerUrl}");
        Console.WriteLine("Press Ctrl+C to stop.");
        Console.WriteLine(
            "Commands: base <temp>, add, remove <index>, enable <index|all>, disable <index|all>, scenario <auto|idle|heating|cooling>, list, status"
        );

        var commandTask = options.Interactive
            ? Task.Run(() => ReadCommandsAsync(cancellationToken), cancellationToken)
            : Task.CompletedTask;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ProcessCommandsAsync();
                await RefreshServerStateAsync(cancellationToken);

                while (port.BytesToRead > 0)
                {
                    var command = port.ReadLine().Trim();
                    if (command.Equals("info", StringComparison.OrdinalIgnoreCase))
                    {
                        WriteGatewayInfo(port);
                    }
                }

                ApplyScenario();

                foreach (var sensor in sensors.ActiveSensors)
                {
                    WriteSensorPacket(port, sensor);
                }

                await Task.Delay(options.Interval, cancellationToken);
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
            {
                return;
            }

            await commandChannel.Writer.WriteAsync(ParseCommand(line), cancellationToken);
        }
    }

    private async Task ProcessCommandsAsync()
    {
        while (commandChannel.Reader.TryRead(out var command))
        {
            await gate.WaitAsync();
            try
            {
                switch (command)
                {
                    case SetBaseTempCommand setBaseTemp:
                        UpdateBaseTemperature(setBaseTemp.TemperatureC);
                        break;
                    case AddSensorCommand:
                        AddSensor();
                        break;
                    case RemoveSensorCommand removeSensor:
                        RemoveSensor(removeSensor.Index);
                        break;
                    case ToggleSensorCommand toggleSensor:
                        ToggleSensor(toggleSensor.Index, toggleSensor.Enabled);
                        break;
                    case ToggleAllSensorsCommand toggleAllSensors:
                        ToggleAllSensors(toggleAllSensors.Enabled);
                        break;
                    case SetScenarioCommand setScenario:
                        scenario = setScenario.Scenario;
                        Console.WriteLine($"Scenario set to {scenario}.");
                        break;
                    case ListSensorsCommand:
                        ListSensors();
                        break;
                    case StatusCommand:
                        PrintStatus();
                        break;
                }
            }
            finally
            {
                gate.Release();
            }
        }
    }

    private async Task RefreshServerStateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ServerUrl))
        {
            return;
        }

        try
        {
            var thermostat = await HttpClient.GetFromJsonAsync<ThermostatSnapshot>(
                new Uri(new Uri(options.ServerUrl), "/api/thermostat"),
                cancellationToken
            );
            var control = await HttpClient.GetFromJsonAsync<ControlStateSnapshot>(
                new Uri(new Uri(options.ServerUrl), "/api/control/state"),
                cancellationToken
            );
            gatewayStatus = gatewayStatus with { Thermostat = thermostat, Control = control };
        }
        catch
        {
            // The simulator should keep running even if Kelvin is temporarily offline.
        }
    }

    private void ApplyScenario()
    {
        var activeScenario = scenario == SimulatorScenario.Auto ? ResolveAutoScenario() : scenario;

        sensors.StepAll(baseTemperatureC, activeScenario);
    }

    private SimulatorScenario ResolveAutoScenario()
    {
        var mode = gatewayStatus.Thermostat?.Mode;
        return mode switch
        {
            "Heating" => SimulatorScenario.Heating,
            "Cooling" => SimulatorScenario.Cooling,
            "Off" => SimulatorScenario.Idle,
            "Disabled" => SimulatorScenario.Idle,
            _ when gatewayStatus.Control?.CallState is "Heating" => SimulatorScenario.Heating,
            _ when gatewayStatus.Control?.CallState is "Cooling" => SimulatorScenario.Cooling,
            _ => SimulatorScenario.Idle,
        };
    }

    private void UpdateBaseTemperature(float temperatureC)
    {
        baseTemperatureC = temperatureC;
        Console.WriteLine(
            $"Base temperature set to {temperatureC.ToString("F1", CultureInfo.InvariantCulture)}C."
        );
    }

    private void AddSensor()
    {
        var sensor = sensors.AddSensor(baseTemperatureC);
        Console.WriteLine($"Sensor added. Total sensors: {sensors.Count}. Added {sensor}.");
    }

    private void RemoveSensor(int index)
    {
        if (!sensors.RemoveSensor(index, out var removedSensor))
        {
            Console.WriteLine("Invalid sensor index.");
            return;
        }

        Console.WriteLine(
            $"Sensor {index} removed. Total sensors: {sensors.Count}. Removed {removedSensor}."
        );
    }

    private void ToggleSensor(int index, bool enabled)
    {
        if (!sensors.SetSensorEnabled(index, enabled, out var sensor))
        {
            Console.WriteLine("Invalid sensor index.");
            return;
        }

        Console.WriteLine($"Sensor {index} {(enabled ? "enabled" : "disabled")}. {sensor}");
    }

    private void ToggleAllSensors(bool enabled)
    {
        var count = sensors.SetAllSensorsEnabled(enabled);
        Console.WriteLine($"{count} sensor(s) {(enabled ? "enabled" : "disabled")}. ");
    }

    private void ListSensors()
    {
        for (var index = 0; index < sensors.Count; index++)
        {
            Console.WriteLine(sensors.Describe(index));
        }
    }

    private void PrintStatus()
    {
        Console.WriteLine($"Scenario: {scenario}");
        Console.WriteLine($"Thermostat mode: {gatewayStatus.Thermostat?.Mode ?? "unknown"}");
        Console.WriteLine($"Control call: {gatewayStatus.Control?.CallState ?? "unknown"}");
        Console.WriteLine($"Sensors: {sensors.Count}");
        Console.WriteLine(
            $"Base temperature: {baseTemperatureC.ToString("F1", CultureInfo.InvariantCulture)}C"
        );
    }

    private static SimulatorCommand ParseCommand(string line)
    {
        var parts = line.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        if (parts.Length == 0)
        {
            return new StatusCommand();
        }

        return parts[0].ToLowerInvariant() switch
        {
            "base"
                when parts.Length > 1
                    && float.TryParse(parts[1], CultureInfo.InvariantCulture, out var temp) =>
                new SetBaseTempCommand(temp),
            "add" => new AddSensorCommand(),
            "remove" when parts.Length > 1 && int.TryParse(parts[1], out var removeIndex) =>
                new RemoveSensorCommand(removeIndex),
            "enable" when parts.Length > 1 && int.TryParse(parts[1], out var enableIndex) =>
                new ToggleSensorCommand(enableIndex, true),
            "disable" when parts.Length > 1 && int.TryParse(parts[1], out var disableIndex) =>
                new ToggleSensorCommand(disableIndex, false),
            "enable"
                when parts.Length > 1
                    && parts[1].Equals("all", StringComparison.OrdinalIgnoreCase) =>
                new ToggleAllSensorsCommand(true),
            "disable"
                when parts.Length > 1
                    && parts[1].Equals("all", StringComparison.OrdinalIgnoreCase) =>
                new ToggleAllSensorsCommand(false),
            "scenario"
                when parts.Length > 1
                    && Enum.TryParse<SimulatorScenario>(parts[1], true, out var scenario) =>
                new SetScenarioCommand(scenario),
            "list" => new ListSensorsCommand(),
            _ => new StatusCommand(),
        };
    }

    private static void WriteGatewayInfo(SerialPort port)
    {
        var infoHeader = new[] { InfoHeaderFirst, InfoHeaderSecond };
        port.Write(infoHeader, 0, infoHeader.Length);
        port.Write(GatewayMac, 0, GatewayMac.Length);
    }

    private static void WriteSensorPacket(SerialPort port, SimulatedSensor sensor)
    {
        var payload = new byte[2 + MacLength + PayloadLength];
        payload[0] = PacketHeaderFirst;
        payload[1] = PacketHeaderSecond;
        sensor.MacAddress.CopyTo(payload, 2);

        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(8, 4), sensor.TemperatureC);
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(12, 4), sensor.HumidityPercentage);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(16, 2), sensor.CO2LevelPpm);
        BinaryPrimitives.WriteSingleLittleEndian(
            payload.AsSpan(20, 4),
            sensor.BatteryLevelPercentage
        );

        port.Write(payload, 0, payload.Length);
        Console.WriteLine(sensor);
    }
}
