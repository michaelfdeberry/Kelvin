namespace Kelvin.Protocol;

/// <summary>
/// The 9-byte header prefixed to every device message (after the frame tag), both directions: message type,
/// chunk index, chunk count, this frame's payload length (uint16 LE), and a message ID (uint32 LE) shared by
/// all its chunks. Message type values are scoped to each device's frame tag.
/// </summary>
public static class DeviceEnvelope
{
    public const int HeaderSize = 9;

    // ESP-NOW's practical payload ceiling is ~250 bytes; leave headroom for this header and the 4 byte frame tag.
    private const int MaxChunkPayloadSize = 195;
    public const int MaxPayloadSize = MaxChunkPayloadSize * byte.MaxValue;

    private static int nextMessageId = Random.Shared.Next();

    public static List<byte[]> Encode(byte type, byte[] payload)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(payload.Length, MaxPayloadSize);
        var messageId = unchecked((uint)Interlocked.Increment(ref nextMessageId));
        var chunkCount = (byte)
            Math.Max(1, (int)Math.Ceiling(payload.Length / (double)MaxChunkPayloadSize));
        var frames = new List<byte[]>(chunkCount);

        for (byte chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            var start = chunkIndex * MaxChunkPayloadSize;
            var length = Math.Min(MaxChunkPayloadSize, payload.Length - start);
            var frame = new byte[HeaderSize + length];
            frame[0] = type;
            frame[1] = chunkIndex;
            frame[2] = chunkCount;
            BitConverter.TryWriteBytes(frame.AsSpan(3, 2), (ushort)length);
            BitConverter.TryWriteBytes(frame.AsSpan(5, 4), messageId);
            Buffer.BlockCopy(payload, start, frame, HeaderSize, length);
            frames.Add(frame);
        }

        return frames;
    }

    public static bool TryDecode(
        ReadOnlySpan<byte> frame,
        out byte type,
        out byte chunkIndex,
        out byte chunkCount,
        out uint messageId,
        out ReadOnlySpan<byte> payload
    )
    {
        type = 0;
        chunkIndex = 0;
        chunkCount = 0;
        messageId = 0;
        payload = default;
        if (frame.Length < HeaderSize)
            return false;

        var length = BitConverter.ToUInt16(frame[3..5]);
        if (frame[2] == 0 || frame[1] >= frame[2] || frame.Length != HeaderSize + length)
            return false;

        type = frame[0];
        chunkIndex = frame[1];
        chunkCount = frame[2];
        messageId = BitConverter.ToUInt32(frame[5..9]);
        payload = frame.Slice(HeaderSize, length);
        return true;
    }
}
