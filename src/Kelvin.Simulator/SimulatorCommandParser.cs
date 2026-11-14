using System.Globalization;

namespace Kelvin.Simulator;

internal static class SimulatorCommandParser
{
    public static SimulatorCommand Parse(string line)
    {
        var parts = line.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        if (parts.Length == 0)
            return new StatusCommand();

        return parts[0].ToLowerInvariant() switch
        {
            "base" when parts.Length > 1 && TryParseFloat(parts[1], out var temp) =>
                new SetBaseTempCommand(temp),
            "base"
                when parts.Length == 3
                    && parts[1].Equals("humidity", StringComparison.OrdinalIgnoreCase)
                    && TryParseFloat(parts[2], out var humidity)
                    && float.IsFinite(humidity)
                    && humidity is >= 0f and <= 100f => new SetBaseHumidityCommand(humidity),
            "base"
                when parts.Length == 3
                    && parts[1].Equals("co2", StringComparison.OrdinalIgnoreCase)
                    && ushort.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var co2) =>
                new SetBaseCo2Command(co2),
            "base"
                when parts.Length > 1
                    && (parts[1].Equals("humidity", StringComparison.OrdinalIgnoreCase)
                        || parts[1].Equals("co2", StringComparison.OrdinalIgnoreCase)) =>
                new InvalidBaselineCommand(),
            "add" => new AddSensorCommand(),
            "remove" when parts.Length > 1 && int.TryParse(parts[1], out var removeIndex) =>
                new RemoveSensorCommand(removeIndex),
            "enable" when parts.Length > 1 && int.TryParse(parts[1], out var enableIndex) =>
                new ToggleSensorCommand(enableIndex, true),
            "disable" when parts.Length > 1 && int.TryParse(parts[1], out var disableIndex) =>
                new ToggleSensorCommand(disableIndex, false),
            "enable" when parts.Length > 1 && IsAll(parts[1]) => new ToggleAllSensorsCommand(true),
            "disable" when parts.Length > 1 && IsAll(parts[1]) => new ToggleAllSensorsCommand(
                false
            ),
            "scenario"
                when parts.Length > 1
                    && Enum.TryParse<SimulatorScenario>(parts[1], true, out var scenario) =>
                new SetScenarioCommand(scenario),
            "list" => new ListSensorsCommand(),
            "humidifier"
                when parts.Length == 2
                    && parts[1].Equals("add", StringComparison.OrdinalIgnoreCase) =>
                new AddHumidifierCommand(),
            "humidifier"
                when parts.Length == 2
                    && parts[1].Equals("remove", StringComparison.OrdinalIgnoreCase) =>
                new RemoveHumidifierCommand(),
            "hmi" => ParseHmi(parts[1..]),
            _ => new StatusCommand(),
        };
    }

    private static SimulatorCommand ParseHmi(string[] parts)
    {
        if (parts.Length == 0)
            return new ListHmisCommand();

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
            return fleetCommand;

        var index = 0;
        if (int.TryParse(parts[0], out var parsedIndex))
        {
            index = parsedIndex;
            parts = parts[1..];
        }

        if (parts.Length == 0)
            return new HmiStateCommand(index);

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
                    && TryParseEnum<HmiRunType>(args[0], out var type)
                    && TryParseFloat(args[1], out var temp) => new HmiSetSetPointCommand(
                index,
                type,
                temp
            ),
            "schedule" => ParseSchedule(index, args),
            "lockouts"
                when args.Length == 2
                    && TryParseOptionalFloat(args[0], out var heating)
                    && TryParseOptionalFloat(args[1], out var cooling) => new HmiSetLockoutsCommand(
                index,
                heating,
                cooling
            ),
            _ => new HmiHelpCommand(),
        };
    }

    private static SimulatorCommand ParseSchedule(int index, string[] args)
    {
        if (args.Length == 0)
            return new HmiHelpCommand();

        var verb = args[0].ToLowerInvariant();
        if (verb == "remove" && args.Length == 2 && int.TryParse(args[1], out var removeIndex))
            return new HmiRemoveScheduleCommand(index, removeIndex);

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
            return new HmiUpsertScheduleCommand(index, scheduleIndex, type, start, end, temp);

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
            return true;
        if (!TryParseFloat(value, out var parsed))
            return false;
        result = parsed;
        return true;
    }

    private static bool TryParseOnOff(string value, out bool enabled)
    {
        enabled = value.Equals("on", StringComparison.OrdinalIgnoreCase);
        return enabled || value.Equals("off", StringComparison.OrdinalIgnoreCase);
    }
}
