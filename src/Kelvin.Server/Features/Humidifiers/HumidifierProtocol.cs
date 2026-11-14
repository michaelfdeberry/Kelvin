using Kelvin.Protocol;

namespace Kelvin.Server.Features.Humidifiers;

/// <summary>
/// Mirrors Kelvin.Devices/Common/src/HumidifierProtocol.h - keep in sync.
/// </summary>
public enum HumidifierMessageType : byte
{
  /// <summary>Humidifier -> Server, at boot and every 5 minutes. Body: {0: version(1), 1: relayOn(bool)}.</summary>
  CheckIn = 1,

  /// <summary>Server -> Humidifier. Body: {0: version(1), 1: enabled(bool)}.</summary>
  State = 2,

  /// <summary>Humidifier -> Server, after applying a State. Body: {0: version(1), 1: relayOn(bool)}.</summary>
  StateAck = 3,
}

/// <summary>
/// Humidifier frames (after the KHUM frame tag) are a <see cref="DeviceEnvelope" /> around a versioned CBOR map.
/// Every message fits a single chunk.
/// </summary>
public static class HumidifierProtocol
{
  public static byte[] EncodeState(bool enabled) =>
    HmiCbor.Encode(
      1,
      writer =>
      {
        writer.WriteUInt32(1);
        writer.WriteBoolean(enabled);
      }
    );

  public static bool TryDecode(byte[] frame, out HumidifierMessageType type, out bool relayOn)
  {
    type = default;
    relayOn = false;
    if (
      !DeviceEnvelope.TryDecode(frame, out var rawType, out _, out var chunkCount, out _, out var body)
      || chunkCount != 1
      || rawType is not ((byte)HumidifierMessageType.CheckIn or (byte)HumidifierMessageType.StateAck)
    )
      return false;

    try
    {
      relayOn = HmiCborMap.Parse(body.ToArray()).Boolean(1);
      type = (HumidifierMessageType)rawType;
      return true;
    }
    catch (Exception exception) when (HmiCbor.IsDecodeError(exception))
    {
      return false;
    }
  }
}
