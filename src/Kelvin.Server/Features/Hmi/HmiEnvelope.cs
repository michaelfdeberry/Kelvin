using Kelvin.Server.Models;

namespace Kelvin.Server.Features.Hmi;

/// <summary>
/// The 5-byte header prefixed to every Hmi message, both directions: message type, chunk index, chunk
/// count, and this frame's payload length. Replaces each encoder/decoder hand-rolling its own framing (only
/// ThermostatStateChunk previously chunked; ControlStateChanged/EnvironmentReadingChanged/uplink commands
/// had no chunk header at all) with one shared, chunk-capable envelope every message type goes through.
/// </summary>
public static class HmiEnvelope
{
  public const int HeaderSize = 5;

  // ESP-NOW's practical payload ceiling is ~250 bytes; leave headroom for this header plus the 4 byte
  // frame tag IHmiOutboundChannel prepends before this reaches the radio.
  private const int MaxChunkPayloadSize = 195;

  public static List<byte[]> Encode(HmiMessageType type, byte[] payload)
  {
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
      Buffer.BlockCopy(payload, start, frame, HeaderSize, length);
      frames.Add(frame);
    }

    return frames;
  }

  public static bool TryDecode(byte[] frame, out HmiMessageType type, out byte chunkIndex, out byte chunkCount, out ReadOnlySpan<byte> payload)
  {
    if (frame.Length < HeaderSize)
    {
      type = default;
      chunkIndex = 0;
      chunkCount = 0;
      payload = default;
      return false;
    }

    type = (HmiMessageType)frame[0];
    chunkIndex = frame[1];
    chunkCount = frame[2];
    var length = BitConverter.ToUInt16(frame, 3);

    if (frame.Length < HeaderSize + length)
    {
      payload = default;
      return false;
    }

    payload = frame.AsSpan(HeaderSize, length);
    return true;
  }
}
