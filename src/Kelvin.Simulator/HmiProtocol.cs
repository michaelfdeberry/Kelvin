using System.Globalization;
using System.Text;
using Kelvin.Protocol;

namespace Kelvin.Simulator;

// Mirrors Kelvin.Server's Models/HmiMessageType.cs ordinals - keep in sync.
internal enum HmiMessageType : byte
{
    ThermostatStateChunk = 1,
    SensorReading = 2,
    ControlStateChanged = 3,
    EnvironmentReadingChanged = 4,
    PreferencesChanged = 5,
    SetMode = 0x10,
    SetFanEnabled = 0x11,
    SetSetPoint = 0x12,
    UpsertSchedule = 0x13,
    RemoveSchedule = 0x14,
    SetForecastLockouts = 0x15,
    RequestState = 0x16,
}

// Mirrors Kelvin.Server's Models/RunMode.cs ordinals.
internal enum HmiRunMode : byte
{
    Disabled,
    Off,
    Heating,
    Cooling,
    Automatic,
}

// Mirrors Kelvin.Server's Models/RunType.cs ordinals.
internal enum HmiRunType : byte
{
    Heating,
    Cooling,
}

// Mirrors Kelvin.Server's Models/ControlMessage.cs ControlState ordinals.
internal enum HmiControlState : byte
{
    Disable,
    Enable,
    Dwell,
    Heating,
    Cooling,
    FanOn,
    FanOff,
    Startup,
    Fault,
}

internal sealed record HmiSetPoint(HmiRunType Type, float TargetTemperatureC);

internal sealed record HmiSchedule(
    Guid Id,
    HmiRunType Type,
    TimeOnly Start,
    TimeOnly End,
    float TargetTemperatureC
);

internal sealed record HmiThermostatState(
    HmiRunMode Mode,
    bool FanEnabled,
    float HysteresisC,
    float? HeatingLockoutC,
    float? CoolingLockoutC,
    IReadOnlyList<HmiSetPoint> SetPoints,
    IReadOnlyList<HmiSchedule> Schedules
);

internal sealed record HmiControlStateMessage(
    HmiControlState State,
    float? EnvironmentTemperatureC,
    float? TargetTemperatureC,
    float? HumidityPercentage,
    float? CO2LevelPpm
);

internal sealed record HmiEnvironmentReading(
    float TemperatureC,
    float HumidityPercentage,
    float CO2LevelPpm
);

internal sealed record HmiPreferences(uint TemperatureUnit, uint TimeFormat);

/// <summary>
/// Hand-mirrored copy of the Hmi wire protocol (Kelvin.Server's Features/Hmi/HmiEnvelope.cs, the Hmi
/// encoders, and ReceiveHmiCommandHandler). Envelope integers are little-endian; bodies are CBOR.
/// </summary>
internal static class HmiProtocol
{
    public const int EnvelopeHeaderSize = 9;
    private static int nextMessageId = Random.Shared.Next();

    // Mirrors Kelvin.Server's Models/FrameTags.Hmi.
    public static readonly byte[] FrameTag = [0x4B, 0x48, 0x4D, 0x49]; // "KHMI"

    // Uplink messages are always small enough for a single chunk.
    public static byte[] Envelope(HmiMessageType type, byte[] body)
    {
        var frame = new byte[EnvelopeHeaderSize + body.Length];
        frame[0] = (byte)type;
        frame[1] = 0;
        frame[2] = 1;
        BitConverter.TryWriteBytes(frame.AsSpan(3, 2), (ushort)body.Length);
        BitConverter.TryWriteBytes(
            frame.AsSpan(5, 4),
            unchecked((uint)Interlocked.Increment(ref nextMessageId))
        );
        body.CopyTo(frame, EnvelopeHeaderSize);
        return frame;
    }

    public static bool TryReadEnvelope(
        ReadOnlySpan<byte> frame,
        out HmiMessageType type,
        out byte chunkIndex,
        out byte chunkCount,
        out uint messageId,
        out byte[] body
    )
    {
        type = default;
        chunkIndex = 0;
        chunkCount = 0;
        messageId = 0;
        body = [];

        if (frame.Length < EnvelopeHeaderSize)
        {
            return false;
        }

        var length = BitConverter.ToUInt16(frame[3..5]);
        if (frame[2] == 0 || frame[1] >= frame[2] || frame.Length != EnvelopeHeaderSize + length)
        {
            return false;
        }

        type = (HmiMessageType)frame[0];
        chunkIndex = frame[1];
        chunkCount = frame[2];
        messageId = BitConverter.ToUInt32(frame[5..9]);
        body = frame.Slice(EnvelopeHeaderSize, length).ToArray();
        return true;
    }

    public static byte[] EncodeSensorReading(
        float temperatureC,
        float humidityPercentage,
        float batteryLevelPercentage
    ) =>
        HmiCbor.Encode(
            4,
            writer =>
            {
                writer.WriteUInt32(1);
                HmiCbor.WriteFloat(writer, temperatureC);
                writer.WriteUInt32(2);
                HmiCbor.WriteFloat(writer, humidityPercentage);
                writer.WriteUInt32(3);
                writer.WriteUInt32(0);
                writer.WriteUInt32(4);
                HmiCbor.WriteFloat(writer, batteryLevelPercentage);
            }
        );

    public static byte[] EncodeSetMode(HmiRunMode mode) =>
        HmiCbor.Encode(
            1,
            writer =>
            {
                writer.WriteUInt32(1);
                writer.WriteUInt32((uint)mode);
            }
        );

    public static byte[] EncodeSetFanEnabled(bool enabled) =>
        HmiCbor.Encode(
            1,
            writer =>
            {
                writer.WriteUInt32(1);
                writer.WriteBoolean(enabled);
            }
        );

    public static byte[] EncodeRequestState() => HmiCbor.Encode(0, _ => { });

    public static byte[] EncodeSetSetPoint(HmiRunType type, float targetTemperatureC) =>
        HmiCbor.Encode(
            2,
            writer =>
            {
                writer.WriteUInt32(1);
                writer.WriteUInt32((uint)type);
                writer.WriteUInt32(2);
                HmiCbor.WriteFloat(writer, targetTemperatureC);
            }
        );

    public static byte[] EncodeUpsertSchedule(
        Guid? id,
        HmiRunType type,
        TimeOnly start,
        TimeOnly end,
        float targetTemperatureC
    ) =>
        HmiCbor.Encode(
            5,
            writer =>
            {
                writer.WriteUInt32(1);
                if (id.HasValue)
                {
                    writer.WriteByteString(id.Value.ToByteArray());
                }
                else
                    writer.WriteNull();
                writer.WriteUInt32(2);
                writer.WriteUInt32((uint)type);
                writer.WriteUInt32(3);
                writer.WriteUInt32((uint)(start.Hour * 60 + start.Minute));
                writer.WriteUInt32(4);
                writer.WriteUInt32((uint)(end.Hour * 60 + end.Minute));
                writer.WriteUInt32(5);
                HmiCbor.WriteFloat(writer, targetTemperatureC);
            }
        );

    public static byte[] EncodeRemoveSchedule(Guid id) =>
        HmiCbor.Encode(
            1,
            writer =>
            {
                writer.WriteUInt32(1);
                writer.WriteByteString(id.ToByteArray());
            }
        );

    public static byte[] EncodeSetForecastLockouts(
        float? heatingLockoutC,
        float? coolingLockoutC
    ) =>
        HmiCbor.Encode(
            2,
            writer =>
            {
                writer.WriteUInt32(1);
                HmiCbor.WriteNullableFloat(writer, heatingLockoutC);
                writer.WriteUInt32(2);
                HmiCbor.WriteNullableFloat(writer, coolingLockoutC);
            }
        );

    public static HmiThermostatState? DecodeThermostatState(byte[] body) =>
        Read(
            body,
            reader =>
            {
                var mode = (HmiRunMode)reader.UInt(1, 4);
                var fanEnabled = reader.Boolean(2);
                var hysteresisC = reader.Float(3);
                var heatingLockoutC = reader.NullableFloat(4);
                var coolingLockoutC = reader.NullableFloat(5);
                var setPoints = reader
                    .Array(6, 2)
                    .Select(entry => new HmiSetPoint((HmiRunType)entry.UInt(1, 1), entry.Float(2)))
                    .ToList();
                var schedules = reader
                    .Array(7, 32)
                    .Select(entry => new HmiSchedule(
                        entry.Id(1)!.Value,
                        (HmiRunType)entry.UInt(2, 1),
                        FromMinutes(entry.UInt(3, 1439)),
                        FromMinutes(entry.UInt(4, 1439)),
                        entry.Float(5)
                    ))
                    .ToList();

                return new HmiThermostatState(
                    mode,
                    fanEnabled,
                    hysteresisC,
                    heatingLockoutC,
                    coolingLockoutC,
                    setPoints,
                    schedules
                );
            }
        );

    public static HmiControlStateMessage? DecodeControlState(byte[] body) =>
        Read(
            body,
            reader => new HmiControlStateMessage(
                (HmiControlState)reader.UInt(1, 8),
                reader.NullableFloat(2),
                reader.NullableFloat(3),
                reader.NullableFloat(4),
                reader.NullableFloat(5)
            )
        );

    public static HmiEnvironmentReading? DecodeEnvironmentReading(byte[] body) =>
        Read(
            body,
            reader => new HmiEnvironmentReading(reader.Float(1), reader.Float(2), reader.Float(3))
        );

    public static HmiPreferences? DecodePreferences(byte[] body) =>
        Read(body, reader => new HmiPreferences(reader.UInt(1, 1), reader.UInt(2, 1)));

    public static string Describe(HmiThermostatState state)
    {
        var builder = new StringBuilder();
        builder.Append(
            $"ThermostatState mode={state.Mode} fan={(state.FanEnabled ? "on" : "off")} "
                + $"hysteresis={Format(state.HysteresisC)}C heatingLockout={Format(state.HeatingLockoutC)} "
                + $"coolingLockout={Format(state.CoolingLockoutC)}"
        );

        foreach (var setPoint in state.SetPoints)
        {
            builder.Append(
                $"\n    setpoint {setPoint.Type} {Format(setPoint.TargetTemperatureC)}C"
            );
        }

        for (var index = 0; index < state.Schedules.Count; index++)
        {
            var schedule = state.Schedules[index];
            builder.Append(
                $"\n    schedule [{index}] {schedule.Type} {schedule.Start:HH\\:mm}-{schedule.End:HH\\:mm} "
                    + $"{Format(schedule.TargetTemperatureC)}C id={schedule.Id}"
            );
        }

        return builder.ToString();
    }

    public static string Describe(HmiControlStateMessage state) =>
        $"ControlStateChanged state={state.State} environment={Format(state.EnvironmentTemperatureC)}C "
        + $"target={Format(state.TargetTemperatureC)}C humidity={Format(state.HumidityPercentage)}% "
        + $"co2={Format(state.CO2LevelPpm)}";

    public static string Describe(HmiEnvironmentReading reading) =>
        $"EnvironmentReadingChanged temp={Format(reading.TemperatureC)}C "
        + $"humidity={Format(reading.HumidityPercentage)}% co2={Format(reading.CO2LevelPpm)}";

    private static string Format(float? value) =>
        value?.ToString("F2", CultureInfo.InvariantCulture) ?? "none";

    private static TimeOnly FromMinutes(uint minutes) => new((int)minutes / 60, (int)minutes % 60);

    private static T? Read<T>(byte[] body, Func<HmiCborMap, T> read)
        where T : class
    {
        try
        {
            return read(HmiCborMap.Parse(body));
        }
        catch (Exception ex) when (HmiCbor.IsDecodeError(ex))
        {
            return null;
        }
    }
}
