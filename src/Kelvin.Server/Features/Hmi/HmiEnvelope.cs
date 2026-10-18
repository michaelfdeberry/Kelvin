using Kelvin.Server.Models;

namespace Kelvin.Server.Features.Hmi;

/// <summary>
/// The 9-byte header prefixed to every Hmi message, both directions: message type, chunk index, chunk
/// count, this frame's payload length (uint16 LE), and a message ID (uint32 LE) shared by all its chunks.
/// </summary>
public static class HmiEnvelope
{
  public const int HeaderSize = 9;
  private static int _nextMessageId = Random.Shared.Next();

  // ESP-NOW's practical payload ceiling is ~250 bytes; leave headroom for this header plus the 4 byte
  // frame tag IHmiOutboundChannel prepends before this reaches the radio.
  private const int MaxChunkPayloadSize = 195;
  public const int MaxPayloadSize = MaxChunkPayloadSize * byte.MaxValue;

  public static List<byte[]> Encode(HmiMessageType type, byte[] payload)
  {
    ArgumentOutOfRangeException.ThrowIfGreaterThan(payload.Length, MaxPayloadSize);
    var messageId = unchecked((uint)Interlocked.Increment(ref _nextMessageId));
    var chunkCount = (byte)Math.Max(1, (int)Math.Ceiling(payload.Length / (double)MaxChunkPayloadSize));
    var frames = new List<byte[]>(chunkCount);

    for (byte chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
    {
      var start = chunkIndex * MaxChunkPayloadSize;
      var length = Math.Min(MaxChunkPayloadSize, payload.Length - start);
      var frame = new byte[HeaderSize + length];
      frame[0] = (byte)type;
      frame[1] = chunkIndex;
      frame[2] = chunkCount;
      BitConverter.GetBytes((ushort)length).CopyTo(frame, 3);
      BitConverter.GetBytes(messageId).CopyTo(frame, 5);
      Buffer.BlockCopy(payload, start, frame, HeaderSize, length);
      frames.Add(frame);
    }

    return frames;
  }

  public static bool TryDecode(
    byte[] frame,
    out HmiMessageType type,
    out byte chunkIndex,
    out byte chunkCount,
    out uint messageId,
    out ReadOnlySpan<byte> payload
  )
  {
    type = default;
    chunkIndex = 0;
    chunkCount = 0;
    messageId = 0;
    payload = default;
    if (frame.Length < HeaderSize)
    {
      return false;
    }

    type = (HmiMessageType)frame[0];
    chunkIndex = frame[1];
    chunkCount = frame[2];
    var length = BitConverter.ToUInt16(frame, 3);

    if (chunkCount == 0 || chunkIndex >= chunkCount || frame.Length != HeaderSize + length)
    {
      return false;
    }

    messageId = BitConverter.ToUInt32(frame, 5);
    payload = frame.AsSpan(HeaderSize, length);
    return true;
  }
}
