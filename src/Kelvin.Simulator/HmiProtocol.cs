using System.Globalization;
using System.Text;

namespace Kelvin.Simulator;

// Mirrors Kelvin.Server's Models/HmiMessageType.cs ordinals - keep in sync.
internal enum HmiMessageType : byte
{
    ThermostatStateChunk = 1,
    SensorReading = 2,
    ControlStateChanged = 3,
    EnvironmentReadingChanged = 4,
    SetMode = 0x10,
    SetFanEnabled = 0x11,
    SetSetPoint = 0x12,
    UpsertSchedule = 0x13,
    RemoveSchedule = 0x14,
    SetForecastLockouts = 0x15,
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

/// <summary>
/// Hand-mirrored copy of the Hmi wire protocol (Kelvin.Server's Features/Hmi/HmiEnvelope.cs, the Hmi
/// encoders, and ReceiveHmiCommandHandler's decode layouts). All values are little-endian.
/// </summary>
internal static class HmiProtocol
{
    public const int EnvelopeHeaderSize = 5;

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
        body.CopyTo(frame, EnvelopeHeaderSize);
        return frame;
    }

    public static bool TryReadEnvelope(
        ReadOnlySpan<byte> frame,
        out HmiMessageType type,
        out byte chunkIndex,
        out byte chunkCount,
        out byte[] body
    )
    {
        type = default;
        chunkIndex = 0;
        chunkCount = 0;
        body = [];

        if (frame.Length < EnvelopeHeaderSize)
        {
            return false;
        }

        var length = BitConverter.ToUInt16(frame[3..5]);
        if (frame.Length < EnvelopeHeaderSize + length)
        {
            return false;
        }

        type = (HmiMessageType)frame[0];
        chunkIndex = frame[1];
        chunkCount = frame[2];
        body = frame.Slice(EnvelopeHeaderSize, length).ToArray();
        return true;
    }

    public static byte[] EncodeSensorReading(
        float temperatureC,
        float humidityPercentage,
        float batteryLevelPercentage
    ) =>
        Write(writer =>
        {
            writer.Write(temperatureC);
            writer.Write(humidityPercentage);
            writer.Write((ushort)0); // the panel has no CO2 sensor
            writer.Write(batteryLevelPercentage);
        });

    public static byte[] EncodeSetMode(HmiRunMode mode) => [(byte)mode];

    public static byte[] EncodeSetFanEnabled(bool enabled) => [(byte)(enabled ? 1 : 0)];

    public static byte[] EncodeSetSetPoint(HmiRunType type, float targetTemperatureC) =>
        Write(writer =>
        {
            writer.Write((byte)type);
            writer.Write(targetTemperatureC);
        });

    public static byte[] EncodeUpsertSchedule(
        Guid? id,
        HmiRunType type,
        TimeOnly start,
        TimeOnly end,
        float targetTemperatureC
    ) =>
        Write(writer =>
        {
            writer.Write((byte)(id.HasValue ? 1 : 0));
            if (id.HasValue)
            {
                writer.Write(id.Value.ToByteArray());
            }

            writer.Write((byte)type);
            writer.Write((ushort)(start.Hour * 60 + start.Minute));
            writer.Write((ushort)(end.Hour * 60 + end.Minute));
            writer.Write(targetTemperatureC);
        });

    public static byte[] EncodeRemoveSchedule(Guid id) => id.ToByteArray();

    public static byte[] EncodeSetForecastLockouts(
        float? heatingLockoutC,
        float? coolingLockoutC
    ) =>
        Write(writer =>
        {
            WriteOptionalFloat(writer, heatingLockoutC);
            WriteOptionalFloat(writer, coolingLockoutC);
        });

    public static HmiThermostatState? DecodeThermostatState(byte[] body) =>
        Read(
            body,
            reader =>
            {
                var mode = (HmiRunMode)reader.ReadByte();
                var fanEnabled = reader.ReadByte() != 0;
                var hysteresisC = reader.ReadSingle();
                var heatingLockoutC = ReadOptionalFloat(reader);
                var coolingLockoutC = ReadOptionalFloat(reader);

                var setPoints = new List<HmiSetPoint>();
                var setPointCount = reader.ReadByte();
                for (var index = 0; index < setPointCount; index++)
                {
                    setPoints.Add(
                        new HmiSetPoint((HmiRunType)reader.ReadByte(), reader.ReadSingle())
                    );
                }

                var schedules = new List<HmiSchedule>();
                var scheduleCount = reader.ReadByte();
                for (var index = 0; index < scheduleCount; index++)
                {
                    var id = new Guid(reader.ReadBytes(16));
                    var type = (HmiRunType)reader.ReadByte();
                    var start = FromMinutes(reader.ReadUInt16());
                    var end = FromMinutes(reader.ReadUInt16());
                    schedules.Add(new HmiSchedule(id, type, start, end, reader.ReadSingle()));
                }

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
                (HmiControlState)reader.ReadByte(),
                ReadOptionalFloat(reader),
                ReadOptionalFloat(reader),
                ReadOptionalFloat(reader),
                ReadOptionalFloat(reader)
            )
        );

    public static HmiEnvironmentReading? DecodeEnvironmentReading(byte[] body) =>
        Read(
            body,
            reader => new HmiEnvironmentReading(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle()
            )
        );

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

    private static TimeOnly FromMinutes(ushort minutes) => new(minutes / 60 % 24, minutes % 60);

    private static byte[] Write(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream))
        {
            write(writer);
        }

        return stream.ToArray();
    }

    private static T? Read<T>(byte[] body, Func<BinaryReader, T> read)
        where T : class
    {
        try
        {
            using var reader = new BinaryReader(new MemoryStream(body));
            return read(reader);
        }
        // A truncated Guid surfaces as ArgumentException rather than EndOfStreamException.
        catch (Exception ex) when (ex is EndOfStreamException or ArgumentException)
        {
            return null;
        }
    }

    private static void WriteOptionalFloat(BinaryWriter writer, float? value)
    {
        writer.Write((byte)(value.HasValue ? 1 : 0));
        if (value.HasValue)
        {
            writer.Write(value.Value);
        }
    }

    private static float? ReadOptionalFloat(BinaryReader reader) =>
        reader.ReadByte() != 0 ? reader.ReadSingle() : null;
}
