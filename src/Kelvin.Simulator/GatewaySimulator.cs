using System.Buffers.Binary;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Kelvin.Simulator;

internal sealed class GatewaySimulator
{
    private const byte DeviceUplinkHeaderFirst = 0xAC;
    private const byte DeviceUplinkHeaderSecond = 0x57;
    private const byte DeviceDownlinkHeaderFirst = 0xAD;
    private const byte DeviceDownlinkHeaderSecond = 0x58;
    private const byte InfoHeaderFirst = 0xAB;
    private const byte InfoHeaderSecond = 0x56;
    private const int MacLength = 6;
    private const int PayloadLength = 16;
    private const float DefaultHysteresisC = 0.6f;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    // Mirrors Kelvin.Server's Models/FrameTags.Node - identifies this as a Node-shaped reading to the server.
    private static readonly byte[] NodeFrameTag = [0x4B, 0x4E, 0x4F, 0x44]; // "KNOD"

    private const float AmbientSlewRateCPerMinute = 0.5f;
    private const float HeatingAmbientTargetOffsetC = 1.5f;
    private const float CoolingAmbientTargetOffsetC = -1.5f;
    private static readonly byte[] GatewayMac = [0x02, 0x11, 0x22, 0x33, 0x44, 0x55];
    private static readonly HttpClient HttpClient = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SimulatorOptions options;
    private readonly SensorFleet sensors;
    private readonly HmiFleet hmis;
    private readonly List<byte> receiveBuffer = [];
    private readonly Channel<SimulatorCommand> commandChannel =
        Channel.CreateUnbounded<SimulatorCommand>();
    private readonly SemaphoreSlim gate = new(1, 1);
    private SimulatorScenario scenario = SimulatorScenario.Auto;
    private GatewayStatus gatewayStatus = new();
    private float baseTemperatureC;
    private float ambientTemperatureC;
    private AmbientTrend lastAmbientTrend = AmbientTrend.Neutral;
    private float lastAmbientTargetC;
    private string? lastLoggedCallState;
    private string? lastLoggedMode;
    private bool? lastLoggedFanOn;
    private AmbientTrend? lastLoggedTrend;
    private float? lastLoggedTargetC;

    public GatewaySimulator(SimulatorOptions options)
    {
        this.options = options;
        baseTemperatureC = options.BaseTemperatureC;
        ambientTemperatureC = options.BaseTemperatureC;
        sensors = new SensorFleet(options.SensorCount, baseTemperatureC);
        hmis = new HmiFleet(options.HmiCount, baseTemperatureC);
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
            $"Kelvin simulator connected to {options.PortName} with {sensors.Count} sensor(s) and {hmis.Count} HMI(s)."
        );
        Console.WriteLine($"Kelvin.Server target: {options.ServerUrl}");
        Console.WriteLine("Press Ctrl+C to stop.");
        Console.WriteLine(
            "Commands: base <temp>, add, remove <index>, enable <index|all>, disable <index|all>, scenario <auto|idle|heating|cooling>, list, status, hmi (type 'hmi help')"
        );

        var commandTask = options.Interactive
            ? Task.Run(() => ReadCommandsAsync(cancellationToken), cancellationToken)
            : Task.CompletedTask;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ProcessCommandsAsync(port);
                await RefreshServerStateAsync(cancellationToken);

                ReadIncoming(port);

                ApplyScenario();

                foreach (var sensor in sensors.ActiveSensors)
                {
                    WriteSensorPacket(port, sensor);
                }

                foreach (var hmi in hmis.ActiveHmis)
                {
                    WriteHmiReading(port, hmi);
                }

                await WaitForNextTickAsync(port, cancellationToken);
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

    // Downlink frames and typed commands should show up promptly, not only once per (default 30s) packet interval.
    private async Task WaitForNextTickAsync(SerialPort port, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + options.Interval;
        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            await Task.Delay(
                remaining < PollInterval ? remaining : PollInterval,
                cancellationToken
            );
            ReadIncoming(port);
            await ProcessCommandsAsync(port);
        }
    }

    private async Task ProcessCommandsAsync(SerialPort port)
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
                    case AddHmiCommand:
                        AddHmi();
                        break;
                    case RemoveHmiCommand removeHmi:
                        RemoveHmi(removeHmi.Index);
                        break;
                    case ToggleHmiCommand toggleHmi:
                        ToggleHmi(toggleHmi.Index, toggleHmi.Enabled);
                        break;
                    case ToggleAllHmisCommand toggleAllHmis:
                        ToggleAllHmis(toggleAllHmis.Enabled);
                        break;
                    case ListHmisCommand:
                        ListHmis();
                        break;
                    case HmiHelpCommand:
                        PrintHmiHelp();
                        break;
                    case HmiDeviceCommand hmiCommand:
                        HandleHmiDeviceCommand(port, hmiCommand);
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
            var thermostat = await GetJsonAsync<ThermostatSnapshot>(
                "/api/thermostat",
                cancellationToken
            );
            var control = await GetJsonAsync<ControlStateSnapshot>(
                "/api/control/state",
                cancellationToken
            );
            // /api/control/state's LastChange can be a newer Fan/Control-kind row with no target/hysteresis,
            // so the active call's own target/hysteresis is fetched directly from its history axis.
            var callHistory = await GetJsonAsync<ControlHistoryPageSnapshot>(
                "/api/control/history?kind=Call&pageSize=1",
                cancellationToken
            );
            gatewayStatus = gatewayStatus with
            {
                Thermostat = thermostat,
                Control = control,
                CallContext = callHistory?.Items?.FirstOrDefault(),
            };
        }
        catch (Exception ex)
        {
            // The simulator should keep running even if Kelvin is temporarily offline.
            LogDebug(
                DebugLevel.Info,
                $"RefreshServerStateAsync failed: {ex.GetType().Name}: {ex.Message}"
            );
        }
    }

    private async Task<T?> GetJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        var uri = new Uri(new Uri(options.ServerUrl), path);
        using var response = await HttpClient.GetAsync(uri, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        LogDebug(
            DebugLevel.Verbose,
            $"GET {uri} -> {(int)response.StatusCode} {response.StatusCode}\n{body}"
        );

        if (!response.IsSuccessStatusCode)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(body, JsonOptions);
    }

    private void LogDebug(DebugLevel level, string message)
    {
        if (options.Debug < level)
        {
            return;
        }

        Console.WriteLine($"[debug] {message}");
    }

    private void ApplyScenario()
    {
        var directive = ResolveAmbientDirective();
        lastAmbientTrend = directive.Trend;
        lastAmbientTargetC = directive.TargetTemperatureC;
        LogDirective(directive);
        // The server averages sensor readings (ambient + each device's fixed room offset), so the
        // shared ambient value must aim short/past the real target by the fleet's average offset
        // or the call can stall just shy of the threshold forever.
        StepAmbientTemperature(directive.TargetTemperatureC - FleetBiasC);
        sensors.StepAll(ambientTemperatureC);
        hmis.StepAll(ambientTemperatureC);
    }

    // HMIs report their onboard reading as a sensor too, so their offsets count toward the server's average.
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
                + $"trend={directive.Trend} target={directive.TargetTemperatureC:F2}C ambient={ambientTemperatureC:F2}C "
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

    private AmbientDirective ResolveAmbientDirective()
    {
        if (scenario != SimulatorScenario.Auto)
        {
            var trend = scenario switch
            {
                SimulatorScenario.Heating => AmbientTrend.Warming,
                SimulatorScenario.Cooling => AmbientTrend.Cooling,
                _ => AmbientTrend.Neutral,
            };

            var target = trend switch
            {
                AmbientTrend.Warming => baseTemperatureC + HeatingAmbientTargetOffsetC,
                AmbientTrend.Cooling => baseTemperatureC + CoolingAmbientTargetOffsetC,
                _ => baseTemperatureC,
            };

            return new AmbientDirective(trend, target);
        }

        return ResolveAutoAmbientDirective();
    }

    private AmbientDirective ResolveAutoAmbientDirective()
    {
        var callState = gatewayStatus.Control?.CallState;
        var mode = gatewayStatus.Thermostat?.Mode;
        var targetTemperatureC = gatewayStatus.CallContext?.TargetTemperatureC;
        var hysteresisC =
            gatewayStatus.CallContext?.HysteresisC
            ?? gatewayStatus.Thermostat?.HysteresisC
            ?? DefaultHysteresisC;

        if (callState is "Heating")
        {
            // Drive past the setpoint to the real turn-off threshold, or the call never satisfies.
            return new AmbientDirective(
                AmbientTrend.Warming,
                (targetTemperatureC ?? baseTemperatureC) + hysteresisC
            );
        }

        if (callState is "Cooling")
        {
            return new AmbientDirective(
                AmbientTrend.Cooling,
                (targetTemperatureC ?? baseTemperatureC) - hysteresisC
            );
        }

        return mode switch
        {
            // No active cooling call means the environment should drift warmer again.
            "Cooling" => new AmbientDirective(
                AmbientTrend.Warming,
                (targetTemperatureC ?? baseTemperatureC) + hysteresisC
            ),
            // No active heating call means the environment should drift cooler again.
            "Heating" => new AmbientDirective(
                AmbientTrend.Cooling,
                (targetTemperatureC ?? baseTemperatureC) - hysteresisC
            ),
            "Off" => new AmbientDirective(
                AmbientTrend.Neutral,
                targetTemperatureC ?? baseTemperatureC
            ),
            "Disabled" => new AmbientDirective(
                AmbientTrend.Neutral,
                targetTemperatureC ?? baseTemperatureC
            ),
            _ => new AmbientDirective(AmbientTrend.Neutral, targetTemperatureC ?? baseTemperatureC),
        };
    }

    private void StepAmbientTemperature(float targetAmbientTemperature)
    {
        var maxDeltaThisStep = (float)(AmbientSlewRateCPerMinute * options.Interval.TotalMinutes);
        if (maxDeltaThisStep <= 0)
        {
            return;
        }

        ambientTemperatureC = MoveTowards(
            ambientTemperatureC,
            targetAmbientTemperature,
            maxDeltaThisStep
        );
    }

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        if (Math.Abs(target - current) <= maxDelta)
        {
            return target;
        }

        return current + MathF.Sign(target - current) * maxDelta;
    }

    private sealed record AmbientDirective(AmbientTrend Trend, float TargetTemperatureC);

    private enum AmbientTrend
    {
        Neutral,
        Warming,
        Cooling,
    }

    private void UpdateBaseTemperature(float temperatureC)
    {
        baseTemperatureC = temperatureC;
        ambientTemperatureC = temperatureC;
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
        Console.WriteLine($"HMIs: {hmis.Count}");
        Console.WriteLine($"Ambient trend: {lastAmbientTrend}");
        Console.WriteLine(
            $"Ambient target: {lastAmbientTargetC.ToString("F1", CultureInfo.InvariantCulture)}C"
        );
        Console.WriteLine(
            $"Base temperature: {baseTemperatureC.ToString("F1", CultureInfo.InvariantCulture)}C"
        );
        Console.WriteLine(
            $"Ambient temperature: {ambientTemperatureC.ToString("F1", CultureInfo.InvariantCulture)}C"
        );
        Console.WriteLine(
            $"Call target: {gatewayStatus.CallContext?.TargetTemperatureC?.ToString("F1", CultureInfo.InvariantCulture) ?? "unknown"}C"
        );
        Console.WriteLine(
            $"Call hysteresis: {gatewayStatus.CallContext?.HysteresisC?.ToString("F2", CultureInfo.InvariantCulture) ?? "unknown"}C"
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
            "hmi" => ParseHmiCommand(parts[1..]),
            _ => new StatusCommand(),
        };
    }

    private static SimulatorCommand ParseHmiCommand(string[] parts)
    {
        if (parts.Length == 0)
        {
            return new ListHmisCommand();
        }

        var fleetCommand = parts[0].ToLowerInvariant() switch
        {
            "add" => new AddHmiCommand(),
            "list" => new ListHmisCommand(),
            "help" => new HmiHelpCommand(),
            "remove" when parts.Length > 1 && int.TryParse(parts[1], out var removeIndex) =>
                new RemoveHmiCommand(removeIndex),
            "enable" when parts.Length > 1 && int.TryParse(parts[1], out var enableIndex) =>
                new ToggleHmiCommand(enableIndex, true),
            "disable" when parts.Length > 1 && int.TryParse(parts[1], out var disableIndex) =>
                new ToggleHmiCommand(disableIndex, false),
            "enable" when parts.Length > 1 && IsAll(parts[1]) => new ToggleAllHmisCommand(true),
            "disable" when parts.Length > 1 && IsAll(parts[1]) => new ToggleAllHmisCommand(false),
            "remove" or "enable" or "disable" => new HmiHelpCommand(),
            _ => (SimulatorCommand?)null,
        };

        if (fleetCommand is not null)
        {
            return fleetCommand;
        }

        // Device commands take an optional leading HMI index, defaulting to the first HMI.
        var index = 0;
        if (int.TryParse(parts[0], out var parsedIndex))
        {
            index = parsedIndex;
            parts = parts[1..];
        }

        if (parts.Length == 0)
        {
            return new HmiStateCommand(index);
        }

        var args = parts[1..];
        return parts[0].ToLowerInvariant() switch
        {
            "state" => new HmiStateCommand(index),
            "reading" => new HmiSendReadingCommand(index),
            "mode" when args.Length == 1 && TryParseEnum<HmiRunMode>(args[0], out var mode) =>
                new HmiSetModeCommand(index, mode),
            "fan" when args.Length == 1 && TryParseOnOff(args[0], out var fanEnabled) =>
                new HmiSetFanCommand(index, fanEnabled),
            "setpoint"
                when args.Length == 2
                    && TryParseEnum<HmiRunType>(args[0], out var setPointType)
                    && TryParseFloat(args[1], out var setPointTemp) => new HmiSetSetPointCommand(
                index,
                setPointType,
                setPointTemp
            ),
            "schedule" => ParseHmiScheduleCommand(index, args),
            "lockouts"
                when args.Length == 2
                    && TryParseOptionalFloat(args[0], out var heatingLockout)
                    && TryParseOptionalFloat(args[1], out var coolingLockout) =>
                new HmiSetLockoutsCommand(index, heatingLockout, coolingLockout),
            _ => new HmiHelpCommand(),
        };
    }

    private static SimulatorCommand ParseHmiScheduleCommand(int index, string[] args)
    {
        if (args.Length == 0)
        {
            return new HmiHelpCommand();
        }

        var verb = args[0].ToLowerInvariant();
        if (verb == "remove" && args.Length == 2 && int.TryParse(args[1], out var removeIndex))
        {
            return new HmiRemoveScheduleCommand(index, removeIndex);
        }

        int? scheduleIndex = null;
        var fields = args[1..];
        if (verb == "update" && args.Length > 1 && int.TryParse(args[1], out var updateIndex))
        {
            scheduleIndex = updateIndex;
            fields = args[2..];
        }
        else if (verb != "add")
        {
            return new HmiHelpCommand();
        }

        if (
            fields.Length == 4
            && TryParseEnum<HmiRunType>(fields[0], out var type)
            && TimeOnly.TryParse(fields[1], CultureInfo.InvariantCulture, out var start)
            && TimeOnly.TryParse(fields[2], CultureInfo.InvariantCulture, out var end)
            && TryParseFloat(fields[3], out var temp)
        )
        {
            return new HmiUpsertScheduleCommand(index, scheduleIndex, type, start, end, temp);
        }

        return new HmiHelpCommand();
    }

    private static bool IsAll(string value) =>
        value.Equals("all", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseEnum<T>(string value, out T result)
        where T : struct, Enum =>
        Enum.TryParse(value, true, out result)
        && !int.TryParse(value, out _)
        && Enum.IsDefined(result);

    private static bool TryParseFloat(string value, out float result) =>
        float.TryParse(value, CultureInfo.InvariantCulture, out result);

    private static bool TryParseOptionalFloat(string value, out float? result)
    {
        result = null;
        if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!TryParseFloat(value, out var parsed))
        {
            return false;
        }

        result = parsed;
        return true;
    }

    private static bool TryParseOnOff(string value, out bool enabled)
    {
        enabled = value.Equals("on", StringComparison.OrdinalIgnoreCase);
        return enabled || value.Equals("off", StringComparison.OrdinalIgnoreCase);
    }

    private void AddHmi()
    {
        var hmi = hmis.AddHmi(baseTemperatureC);
        Console.WriteLine($"HMI added. Total HMIs: {hmis.Count}. Added {hmi}.");
    }

    private void RemoveHmi(int index)
    {
        if (!hmis.RemoveHmi(index, out var removedHmi))
        {
            Console.WriteLine("Invalid HMI index.");
            return;
        }

        Console.WriteLine($"HMI {index} removed. Total HMIs: {hmis.Count}. Removed {removedHmi}.");
    }

    private void ToggleHmi(int index, bool enabled)
    {
        if (!hmis.SetHmiEnabled(index, enabled, out var hmi))
        {
            Console.WriteLine("Invalid HMI index.");
            return;
        }

        Console.WriteLine($"HMI {index} {(enabled ? "enabled" : "disabled")}. {hmi}");
    }

    private void ToggleAllHmis(bool enabled)
    {
        var count = hmis.SetAllHmisEnabled(enabled);
        Console.WriteLine($"{count} HMI(s) {(enabled ? "enabled" : "disabled")}.");
    }

    private void ListHmis()
    {
        for (var index = 0; index < hmis.Count; index++)
        {
            Console.WriteLine(hmis.Describe(index));
        }
    }

    private static void PrintHmiHelp()
    {
        Console.WriteLine(
            """
            HMI fleet commands:
              hmi add | hmi remove <index> | hmi enable <index|all> | hmi disable <index|all> | hmi list
            HMI device commands ([index] defaults to 0, temperatures in C):
              hmi [index] state                                  print the last messages received from the server
              hmi [index] reading                                send the onboard sensor reading now
              hmi [index] mode <disabled|off|heating|cooling|automatic>
              hmi [index] fan <on|off>
              hmi [index] setpoint <heating|cooling> <temp>
              hmi [index] schedule add <heating|cooling> <HH:mm> <HH:mm> <temp>
              hmi [index] schedule update <n> <heating|cooling> <HH:mm> <HH:mm> <temp>
              hmi [index] schedule remove <n>                    <n> is the schedule index shown by 'hmi state'
              hmi [index] lockouts <heatingTemp|none> <coolingTemp|none>
            """
        );
    }

    private void HandleHmiDeviceCommand(SerialPort port, HmiDeviceCommand command)
    {
        if (!hmis.TryGet(command.Index, out var hmi))
        {
            Console.WriteLine("Invalid HMI index.");
            return;
        }

        if (command is HmiStateCommand)
        {
            PrintHmiState(hmi);
            return;
        }

        if (!hmi.Enabled)
        {
            Console.WriteLine($"[{hmi.Label}] is offline; enable it before sending.");
            return;
        }

        switch (command)
        {
            case HmiSendReadingCommand:
                WriteHmiReading(port, hmi);
                break;
            case HmiSetModeCommand setMode:
                WriteHmiMessage(
                    port,
                    hmi,
                    HmiMessageType.SetMode,
                    HmiProtocol.EncodeSetMode(setMode.Mode),
                    $"mode={setMode.Mode}"
                );
                break;
            case HmiSetFanCommand setFan:
                WriteHmiMessage(
                    port,
                    hmi,
                    HmiMessageType.SetFanEnabled,
                    HmiProtocol.EncodeSetFanEnabled(setFan.Enabled),
                    $"fan={(setFan.Enabled ? "on" : "off")}"
                );
                break;
            case HmiSetSetPointCommand setPoint:
                WriteHmiMessage(
                    port,
                    hmi,
                    HmiMessageType.SetSetPoint,
                    HmiProtocol.EncodeSetSetPoint(setPoint.Type, setPoint.TargetTemperatureC),
                    $"type={setPoint.Type} target={FormatTemp(setPoint.TargetTemperatureC)}C"
                );
                break;
            case HmiUpsertScheduleCommand upsert:
                Guid? scheduleId = null;
                if (upsert.ScheduleIndex is { } scheduleIndex)
                {
                    if (!TryGetReceivedSchedule(hmi, scheduleIndex, out var existing))
                    {
                        return;
                    }

                    scheduleId = existing.Id;
                }

                WriteHmiMessage(
                    port,
                    hmi,
                    HmiMessageType.UpsertSchedule,
                    HmiProtocol.EncodeUpsertSchedule(
                        scheduleId,
                        upsert.Type,
                        upsert.Start,
                        upsert.End,
                        upsert.TargetTemperatureC
                    ),
                    $"id={scheduleId?.ToString() ?? "new"} type={upsert.Type} "
                        + $"{upsert.Start:HH\\:mm}-{upsert.End:HH\\:mm} target={FormatTemp(upsert.TargetTemperatureC)}C"
                );
                break;
            case HmiRemoveScheduleCommand remove:
                if (!TryGetReceivedSchedule(hmi, remove.ScheduleIndex, out var removed))
                {
                    return;
                }

                WriteHmiMessage(
                    port,
                    hmi,
                    HmiMessageType.RemoveSchedule,
                    HmiProtocol.EncodeRemoveSchedule(removed.Id),
                    $"id={removed.Id}"
                );
                break;
            case HmiSetLockoutsCommand lockouts:
                WriteHmiMessage(
                    port,
                    hmi,
                    HmiMessageType.SetForecastLockouts,
                    HmiProtocol.EncodeSetForecastLockouts(
                        lockouts.HeatingLockoutC,
                        lockouts.CoolingLockoutC
                    ),
                    $"heating={FormatTemp(lockouts.HeatingLockoutC)} cooling={FormatTemp(lockouts.CoolingLockoutC)}"
                );
                break;
        }
    }

    private static bool TryGetReceivedSchedule(
        SimulatedHmi hmi,
        int scheduleIndex,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out HmiSchedule? schedule
    )
    {
        var schedules = hmi.ThermostatState?.Schedules;
        schedule =
            schedules is not null && scheduleIndex >= 0 && scheduleIndex < schedules.Count
                ? schedules[scheduleIndex]
                : null;

        if (schedule is null)
        {
            Console.WriteLine(
                $"[{hmi.Label}] has no schedule [{scheduleIndex}] in its last received thermostat state (see 'hmi state')."
            );
        }

        return schedule is not null;
    }

    private static void PrintHmiState(SimulatedHmi hmi)
    {
        Console.WriteLine(hmi);
        Console.WriteLine(
            hmi.ThermostatState is null
                ? "  ThermostatState: none received"
                : $"  {HmiProtocol.Describe(hmi.ThermostatState)}"
        );
        Console.WriteLine(
            hmi.ControlState is null
                ? "  ControlStateChanged: none received"
                : $"  {HmiProtocol.Describe(hmi.ControlState)}"
        );
        Console.WriteLine(
            hmi.EnvironmentReading is null
                ? "  EnvironmentReadingChanged: none received"
                : $"  {HmiProtocol.Describe(hmi.EnvironmentReading)}"
        );
    }

    private static string FormatTemp(float? value) =>
        value?.ToString("F1", CultureInfo.InvariantCulture) ?? "none";

    private void ReadIncoming(SerialPort port)
    {
        var available = port.BytesToRead;
        if (available > 0)
        {
            var chunk = new byte[available];
            var read = port.Read(chunk, 0, available);
            receiveBuffer.AddRange(chunk.AsSpan(0, read));
        }

        while (TryProcessNextIncoming(port)) { }
    }

    // The server writes both the newline-terminated "info" probe and binary downlink frames to this port.
    private bool TryProcessNextIncoming(SerialPort port)
    {
        if (receiveBuffer.Count == 0)
        {
            return false;
        }

        if (receiveBuffer[0] == DeviceDownlinkHeaderFirst)
        {
            if (receiveBuffer.Count < 2)
            {
                return false;
            }

            if (receiveBuffer[1] != DeviceDownlinkHeaderSecond)
            {
                receiveBuffer.RemoveAt(0);
                return true;
            }

            const int prefixLength = 2 + MacLength + 2;
            if (receiveBuffer.Count < prefixLength)
            {
                return false;
            }

            var length = receiveBuffer[2 + MacLength] | (receiveBuffer[2 + MacLength + 1] << 8);
            if (receiveBuffer.Count < prefixLength + length)
            {
                return false;
            }

            var macAddress = receiveBuffer.GetRange(2, MacLength).ToArray();
            var payload = receiveBuffer.GetRange(prefixLength, length).ToArray();
            receiveBuffer.RemoveRange(0, prefixLength + length);
            HandleDownlink(macAddress, payload);
            return true;
        }

        var newlineIndex = receiveBuffer.IndexOf((byte)'\n');
        var downlinkIndex = receiveBuffer.IndexOf(DeviceDownlinkHeaderFirst);
        if (downlinkIndex > 0 && (newlineIndex < 0 || downlinkIndex < newlineIndex))
        {
            receiveBuffer.RemoveRange(0, downlinkIndex);
            return true;
        }

        if (newlineIndex < 0)
        {
            return false;
        }

        var line = Encoding.ASCII.GetString([.. receiveBuffer.GetRange(0, newlineIndex)]).Trim();
        receiveBuffer.RemoveRange(0, newlineIndex + 1);
        if (line.Equals("info", StringComparison.OrdinalIgnoreCase))
        {
            WriteGatewayInfo(port);
        }

        return true;
    }

    private void HandleDownlink(byte[] macAddress, byte[] payload)
    {
        var hmi = hmis.FindByMacAddress(macAddress);
        if (hmi is null)
        {
            var mac = string.Join(":", macAddress.Select(byteValue => byteValue.ToString("X2")));
            Console.WriteLine(
                $"[{mac}] <- {payload.Length} byte(s) for an unknown device, dropped."
            );
            return;
        }

        // An offline device is out of radio range, so the gateway's send would never reach it.
        if (!hmi.Enabled)
        {
            LogDebug(DebugLevel.Info, $"[{hmi.Label}] offline, dropped {payload.Length} byte(s).");
            return;
        }

        var tag = HmiProtocol.FrameTag;
        if (
            payload.Length < tag.Length
            || !payload.AsSpan(0, tag.Length).SequenceEqual(tag)
            || !HmiProtocol.TryReadEnvelope(
                payload.AsSpan(tag.Length),
                out var type,
                out var chunkIndex,
                out var chunkCount,
                out var body
            )
        )
        {
            Console.WriteLine($"[{hmi.Label}] <- malformed frame: {Convert.ToHexString(payload)}");
            return;
        }

        LogDebug(
            DebugLevel.Verbose,
            $"[{hmi.Label}] <- {type} chunk {chunkIndex + 1}/{chunkCount} ({body.Length} bytes)"
        );

        if (!hmi.TryReassemble(type, chunkIndex, chunkCount, body, out var message))
        {
            return;
        }

        string? description = null;
        switch (type)
        {
            case HmiMessageType.ThermostatStateChunk:
                hmi.ThermostatState = HmiProtocol.DecodeThermostatState(message);
                description = hmi.ThermostatState is null
                    ? null
                    : HmiProtocol.Describe(hmi.ThermostatState);
                break;
            case HmiMessageType.ControlStateChanged:
                hmi.ControlState = HmiProtocol.DecodeControlState(message);
                description = hmi.ControlState is null
                    ? null
                    : HmiProtocol.Describe(hmi.ControlState);
                break;
            case HmiMessageType.EnvironmentReadingChanged:
                hmi.EnvironmentReading = HmiProtocol.DecodeEnvironmentReading(message);
                description = hmi.EnvironmentReading is null
                    ? null
                    : HmiProtocol.Describe(hmi.EnvironmentReading);
                break;
            default:
                description = $"{type} (unhandled) {Convert.ToHexString(message)}";
                break;
        }

        Console.WriteLine(
            $"[{hmi.Label}] <- {description ?? $"{type} malformed body: {Convert.ToHexString(message)}"}"
        );
    }

    private void WriteHmiReading(SerialPort port, SimulatedHmi hmi)
    {
        var body = HmiProtocol.EncodeSensorReading(
            hmi.TemperatureC,
            hmi.HumidityPercentage,
            hmi.BatteryLevelPercentage
        );
        WriteHmiFrame(port, hmi, HmiMessageType.SensorReading, body);
        Console.WriteLine(hmi);
    }

    private static void WriteHmiMessage(
        SerialPort port,
        SimulatedHmi hmi,
        HmiMessageType type,
        byte[] body,
        string description
    )
    {
        WriteHmiFrame(port, hmi, type, body);
        Console.WriteLine($"[{hmi.Label}] -> {type} {description}");
    }

    private static void WriteHmiFrame(
        SerialPort port,
        SimulatedHmi hmi,
        HmiMessageType type,
        byte[] body
    )
    {
        var envelope = HmiProtocol.Envelope(type, body);
        var contentLength = HmiProtocol.FrameTag.Length + envelope.Length;
        var frame = new byte[2 + MacLength + 2 + contentLength];

        frame[0] = DeviceUplinkHeaderFirst;
        frame[1] = DeviceUplinkHeaderSecond;
        hmi.MacAddress.CopyTo(frame, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(
            frame.AsSpan(2 + MacLength, 2),
            (ushort)contentLength
        );

        var contentOffset = 2 + MacLength + 2;
        HmiProtocol.FrameTag.CopyTo(frame, contentOffset);
        envelope.CopyTo(frame, contentOffset + HmiProtocol.FrameTag.Length);

        port.Write(frame, 0, frame.Length);
    }

    private static void WriteGatewayInfo(SerialPort port)
    {
        var infoHeader = new[] { InfoHeaderFirst, InfoHeaderSecond };
        port.Write(infoHeader, 0, infoHeader.Length);
        port.Write(GatewayMac, 0, GatewayMac.Length);
    }

    private static void WriteSensorPacket(SerialPort port, SimulatedSensor sensor)
    {
        var contentLength = NodeFrameTag.Length + PayloadLength;
        var frame = new byte[2 + MacLength + 2 + contentLength];

        frame[0] = DeviceUplinkHeaderFirst;
        frame[1] = DeviceUplinkHeaderSecond;
        sensor.MacAddress.CopyTo(frame, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(
            frame.AsSpan(2 + MacLength, 2),
            (ushort)contentLength
        );

        var contentOffset = 2 + MacLength + 2;
        NodeFrameTag.CopyTo(frame, contentOffset);

        var payloadOffset = contentOffset + NodeFrameTag.Length;
        BinaryPrimitives.WriteSingleLittleEndian(
            frame.AsSpan(payloadOffset, 4),
            sensor.TemperatureC
        );
        BinaryPrimitives.WriteSingleLittleEndian(
            frame.AsSpan(payloadOffset + 4, 4),
            sensor.HumidityPercentage
        );
        BinaryPrimitives.WriteUInt16LittleEndian(
            frame.AsSpan(payloadOffset + 8, 2),
            sensor.CO2LevelPpm
        );
        BinaryPrimitives.WriteSingleLittleEndian(
            frame.AsSpan(payloadOffset + 12, 4),
            sensor.BatteryLevelPercentage
        );

        port.Write(frame, 0, frame.Length);
        Console.WriteLine(sensor);
    }
}
