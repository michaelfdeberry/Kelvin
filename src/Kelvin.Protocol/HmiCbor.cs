using System.Formats.Cbor;

namespace Kelvin.Protocol;

public static class HmiCbor
{
    public static byte[] Encode(int fieldCount, Action<CborWriter> write)
    {
        var writer = new CborWriter(CborConformanceMode.Strict);
        writer.WriteStartMap(fieldCount + 1);
        writer.WriteUInt32(0);
        writer.WriteUInt32(1);
        write(writer);
        writer.WriteEndMap();
        var payload = writer.Encode();
        if (payload.Length > 2048)
            throw new ArgumentOutOfRangeException(
                nameof(write),
                "HMI payload exceeds the panel reassembly limit."
            );
        return payload;
    }

    public static void WriteFloat(CborWriter writer, float value)
    {
        if (!float.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        writer.WriteSingle(value);
    }

    public static void WriteNullableFloat(CborWriter writer, float? value)
    {
        if (value.HasValue)
            WriteFloat(writer, value.Value);
        else
            writer.WriteNull();
    }

    public static bool IsDecodeError(Exception exception) =>
        exception
            is CborContentException
                or FormatException
                or InvalidOperationException
                or OverflowException
                or ArgumentException;
}

public sealed class HmiCborMap
{
    private readonly Dictionary<uint, ReadOnlyMemory<byte>> fields = new();

    public static HmiCborMap Parse(ReadOnlyMemory<byte> body, bool versioned = true)
    {
        if (body.Length == 0 || body.Length > 2048)
            throw new FormatException("Invalid HMI payload size.");
        var validation = new CborReader(body, CborConformanceMode.Strict);
        ValidateValue(validation, -1);
        if (validation.BytesRemaining != 0)
            throw new FormatException("Trailing CBOR data.");
        var reader = new CborReader(body, CborConformanceMode.Strict);
        var count = reader.ReadStartMap() ?? throw new FormatException("Indefinite map.");
        if (count > 32)
            throw new FormatException("Too many fields.");
        var map = new HmiCborMap();
        for (var index = 0; index < count; index++)
        {
            if (!map.fields.TryAdd(reader.ReadUInt32(), reader.ReadEncodedValue()))
                throw new FormatException("Duplicate field.");
        }
        reader.ReadEndMap();
        if (versioned && map.UInt(0) != 1)
            throw new FormatException("Unsupported HMI schema version.");
        return map;
    }

    private CborReader Reader(uint key) =>
        new(
            fields.TryGetValue(key, out var value)
                ? value
                : throw new FormatException($"Missing field {key}."),
            CborConformanceMode.Strict
        );

    public uint UInt(uint key, uint maximum = uint.MaxValue)
    {
        var value = Reader(key).ReadUInt32();
        return value <= maximum ? value : throw new FormatException("Integer out of range.");
    }

    public bool Boolean(uint key) => Reader(key).ReadBoolean();

    public float Float(uint key)
    {
        var value = (float)Reader(key).ReadDouble();
        return float.IsFinite(value) ? value : throw new FormatException("Non-finite float.");
    }

    public float? NullableFloat(uint key) =>
        Reader(key).PeekState() == CborReaderState.Null ? null : Float(key);

    public Guid? Id(uint key, bool nullable = false)
    {
        var reader = Reader(key);
        if (nullable && reader.PeekState() == CborReaderState.Null)
            return null;
        var bytes = reader.ReadByteString();
        return bytes.Length == 16
            ? new Guid(bytes)
            : throw new FormatException("Invalid schedule ID.");
    }

    public IReadOnlyList<HmiCborMap> Array(uint key, int maximum)
    {
        var reader = Reader(key);
        var count = reader.ReadStartArray() ?? throw new FormatException("Indefinite array.");
        if (count > maximum)
            throw new FormatException("Too many array entries.");
        var result = new List<HmiCborMap>();
        for (var index = 0; index < count; index++)
            result.Add(Parse(reader.ReadEncodedValue(), false));
        reader.ReadEndArray();
        return result;
    }

    private static void ValidateValue(CborReader reader, int depth)
    {
        switch (reader.PeekState())
        {
            case CborReaderState.Tag:
            case CborReaderState.StartIndefiniteLengthByteString:
            case CborReaderState.StartIndefiniteLengthTextString:
                throw new FormatException("Unsupported CBOR item.");
            case CborReaderState.StartArray:
                if (depth >= 4 || reader.ReadStartArray() is not int arrayLength)
                    throw new FormatException("Invalid array nesting.");
                for (var index = 0; index < arrayLength; index++)
                    ValidateValue(reader, depth + 1);
                reader.ReadEndArray();
                break;
            case CborReaderState.StartMap:
                if (depth >= 4 || reader.ReadStartMap() is not int mapLength)
                    throw new FormatException("Invalid map nesting.");
                for (var index = 0; index < mapLength; index++)
                {
                    ValidateValue(reader, depth + 1);
                    ValidateValue(reader, depth + 1);
                }
                reader.ReadEndMap();
                break;
            default:
                reader.SkipValue();
                break;
        }
    }
}
