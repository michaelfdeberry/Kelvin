using System.Globalization;

namespace Kelvin.Simulator;

internal sealed class HmiController(
    HmiFleet hmis,
    Action<byte[]> write,
    Action<string> output,
    Action<DebugLevel, string> log
)
{
    public void Execute(HmiDeviceCommand command)
    {
        if (!hmis.TryGet(command.Index, out var hmi))
        {
            output("Invalid HMI index.");
            return;
        }
        if (command is HmiStateCommand)
        {
            PrintState(hmi);
            return;
        }
        if (!hmi.Enabled)
        {
            output($"[{hmi.Label}] is offline; enable it before sending.");
            return;
        }

        switch (command)
        {
            case HmiSendReadingCommand:
                WriteReading(hmi);
                break;
            case HmiSetModeCommand mode:
                Send(
                    hmi,
                    HmiMessageType.SetMode,
                    HmiProtocol.EncodeSetMode(mode.Mode),
                    $"mode={mode.Mode}"
                );
                break;
            case HmiSetFanCommand fan:
                Send(
                    hmi,
                    HmiMessageType.SetFanEnabled,
                    HmiProtocol.EncodeSetFanEnabled(fan.Enabled),
                    $"fan={(fan.Enabled ? "on" : "off")}"
                );
                break;
            case HmiSetSetPointCommand point:
                Send(
                    hmi,
                    HmiMessageType.SetSetPoint,
                    HmiProtocol.EncodeSetSetPoint(point.Type, point.TargetTemperatureC),
                    $"type={point.Type} target={FormatTemp(point.TargetTemperatureC)}C"
                );
                break;
            case HmiUpsertScheduleCommand upsert:
                Guid? scheduleId = null;
                if (upsert.ScheduleIndex is { } index)
                {
                    if (!TryGetSchedule(hmi, index, out var schedule))
                        return;
                    scheduleId = schedule.Id;
                }
                Send(
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
                if (!TryGetSchedule(hmi, remove.ScheduleIndex, out var removed))
                    return;
                Send(
                    hmi,
                    HmiMessageType.RemoveSchedule,
                    HmiProtocol.EncodeRemoveSchedule(removed.Id),
                    $"id={removed.Id}"
                );
                break;
            case HmiSetLockoutsCommand lockouts:
                Send(
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

    public void WriteReading(SimulatedHmi hmi)
    {
        write(
            GatewayProtocol.EncodeHmiMessage(
                hmi,
                HmiMessageType.SensorReading,
                HmiProtocol.EncodeSensorReading(
                    hmi.TemperatureC,
                    hmi.HumidityPercentage,
                    hmi.BatteryLevelPercentage
                )
            )
        );
        output(hmi.ToString());
    }

    public void Receive(byte[] macAddress, byte[] payload)
    {
        var hmi = hmis.FindByMacAddress(macAddress);
        if (hmi is null)
        {
            var mac = string.Join(":", macAddress.Select(value => value.ToString("X2")));
            output($"[{mac}] <- {payload.Length} byte(s) for an unknown device, dropped.");
            return;
        }
        if (!hmi.Enabled)
        {
            log(DebugLevel.Info, $"[{hmi.Label}] offline, dropped {payload.Length} byte(s).");
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
                out var messageId,
                out var body
            )
        )
        {
            if (payload.Length > tag.Length && payload.AsSpan(0, tag.Length).SequenceEqual(tag))
                hmi.DiscardPending((HmiMessageType)payload[tag.Length]);
            output($"[{hmi.Label}] <- malformed frame: {Convert.ToHexString(payload)}");
            return;
        }

        log(
            DebugLevel.Verbose,
            $"[{hmi.Label}] <- {type} message {messageId} chunk {chunkIndex + 1}/{chunkCount} ({body.Length} bytes)"
        );
        if (!hmi.TryReassemble(type, chunkIndex, chunkCount, messageId, body, out var message))
            return;

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
            case HmiMessageType.PreferencesChanged:
                var preferences = HmiProtocol.DecodePreferences(message);
                if (preferences is not null)
                {
                    hmi.Preferences = preferences;
                    description =
                        $"PreferencesChanged temperatureUnit={preferences.TemperatureUnit} timeFormat={preferences.TimeFormat}";
                }
                break;
            case HmiMessageType.Notify:
                var notification = HmiProtocol.DecodeNotification(message);
                if (notification is not null)
                    description =
                        $"Notify id={notification.Id} type={notification.Type} heading={notification.Heading} message={notification.Message}";
                break;
            case HmiMessageType.DismissNotification:
                var dismissal = HmiProtocol.DecodeNotificationDismissal(message);
                if (dismissal is not null)
                    description = $"DismissNotification id={dismissal.Id}";
                break;
            default:
                description = $"{type} (unhandled) {Convert.ToHexString(message)}";
                break;
        }
        output(
            $"[{hmi.Label}] <- {description ?? $"{type} malformed body: {Convert.ToHexString(message)}"}"
        );
    }

    public void PrintHelp() =>
        output(
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

    private void Send(SimulatedHmi hmi, HmiMessageType type, byte[] body, string description)
    {
        write(GatewayProtocol.EncodeHmiMessage(hmi, type, body));
        output($"[{hmi.Label}] -> {type} {description}");
    }

    private bool TryGetSchedule(
        SimulatedHmi hmi,
        int index,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out HmiSchedule? schedule
    )
    {
        var schedules = hmi.ThermostatState?.Schedules;
        schedule =
            schedules is not null && index >= 0 && index < schedules.Count
                ? schedules[index]
                : null;
        if (schedule is null)
            output(
                $"[{hmi.Label}] has no schedule [{index}] in its last received thermostat state (see 'hmi state')."
            );
        return schedule is not null;
    }

    private void PrintState(SimulatedHmi hmi)
    {
        output(hmi.ToString());
        output(
            hmi.ThermostatState is null
                ? "  ThermostatState: none received"
                : $"  {HmiProtocol.Describe(hmi.ThermostatState)}"
        );
        output(
            hmi.ControlState is null
                ? "  ControlStateChanged: none received"
                : $"  {HmiProtocol.Describe(hmi.ControlState)}"
        );
        output(
            hmi.EnvironmentReading is null
                ? "  EnvironmentReadingChanged: none received"
                : $"  {HmiProtocol.Describe(hmi.EnvironmentReading)}"
        );
    }

    private static string FormatTemp(float? value) =>
        value?.ToString("F1", CultureInfo.InvariantCulture) ?? "none";
}
