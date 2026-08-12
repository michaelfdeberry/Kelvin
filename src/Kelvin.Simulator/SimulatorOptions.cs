namespace Kelvin.Simulator;

internal enum DebugLevel
{
    None,
    Info,
    Verbose,
}

internal sealed record SimulatorOptions(
    string ServerUrl,
    string PortName,
    int SensorCount,
    float BaseTemperatureC,
    TimeSpan Interval,
    bool Interactive,
    DebugLevel Debug
)
{
    public static SimulatorOptions Parse(string[] args)
    {
        var serverUrl = "http://localhost:5000";
        var portName = string.Empty;
        var sensorCount = 5;
        var baseTemperatureC = 21.5f;
        var interval = TimeSpan.FromSeconds(30);
        var interactive = true;
        var debug = DebugLevel.None;

        for (var index = 0; index < args.Length; index++)
        {
            var current = args[index];

            if (TryReadValue(current, "--server-url", args, ref index, out var serverValue))
            {
                serverUrl = serverValue;
                continue;
            }

            if (TryReadValue(current, "--port", args, ref index, out var portValue))
            {
                portName = portValue;
                continue;
            }

            if (
                TryReadValue(current, "--sensor-count", args, ref index, out var sensorValue)
                && int.TryParse(sensorValue, out var parsedSensors)
            )
            {
                sensorCount = Math.Max(1, parsedSensors);
                continue;
            }

            if (
                TryReadValue(current, "--base-temp", args, ref index, out var tempValue)
                && float.TryParse(tempValue, out var parsedTemp)
            )
            {
                baseTemperatureC = parsedTemp;
                continue;
            }

            if (
                TryReadValue(current, "--interval", args, ref index, out var intervalValue)
                && TimeSpan.TryParse(intervalValue, out var parsedInterval)
            )
            {
                interval = parsedInterval;
                continue;
            }

            if (current.Equals("--non-interactive", StringComparison.OrdinalIgnoreCase))
            {
                interactive = false;
                continue;
            }

            if (current.Equals("--debug", StringComparison.OrdinalIgnoreCase))
            {
                debug = DebugLevel.Info;
                continue;
            }

            if (current.StartsWith("--debug=", StringComparison.OrdinalIgnoreCase))
            {
                var levelValue = current["--debug=".Length..];
                debug = Enum.TryParse<DebugLevel>(levelValue, true, out var parsedLevel)
                    ? parsedLevel
                    : DebugLevel.Info;
            }
        }

        if (string.IsNullOrWhiteSpace(portName))
        {
            throw new ArgumentException("A port is required. Use --port COM5.");
        }

        return new SimulatorOptions(
            serverUrl,
            portName,
            sensorCount,
            baseTemperatureC,
            interval,
            interactive,
            debug
        );
    }

    private static bool TryReadValue(
        string current,
        string name,
        string[] args,
        ref int index,
        out string value
    )
    {
        value = string.Empty;

        if (current.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for {name}.");
            }

            value = args[++index];
            return true;
        }

        var prefix = name + "=";
        if (current.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = current[prefix.Length..];
            return true;
        }

        return false;
    }
}
