using Kelvin.Protocol;

namespace Kelvin.Server.Features.Humidifiers;

/// <summary>
/// Mirrors Kelvin.Devices/Common/src/HumidifierProtocol.h - keep in sync.
/// </summary>
public enum HumidifierMessageType : byte
{
  /// <summary>Humidifier -> Server. Body: {0: version(1), 1: relayOn(bool)}.</summary>
  Ping = 1,

  /// <summary>Server -> Humidifier. Body: {0: version(1), 1: enabled(bool)}.</summary>
  State = 2,
}

/// <summary>
/// Humidifier frames (after the KHUM frame tag) are a single message type byte followed by a versioned CBOR map.
/// Every message fits a single ESP-NOW frame, so there is no chunking envelope like the Hmi's.
/// </summary>
public static class HumidifierProtocol
{
  public static byte[] EncodeState(bool enabled)
  {
    var body = HmiCbor.Encode(
      1,
      writer =>
      {
        writer.WriteUInt32(1);
        writer.WriteBoolean(enabled);
      }
    );
    return [(byte)HumidifierMessageType.State, .. body];
  }

  public static bool TryDecodePing(byte[] payload, out bool relayOn)
  {
    relayOn = false;
    if (payload.Length < 2 || payload[0] != (byte)HumidifierMessageType.Ping)
      return false;

    try
    {
      relayOn = HmiCborMap.Parse(payload.AsMemory(1)).Boolean(1);
      return true;
    }
    catch (Exception exception) when (HmiCbor.IsDecodeError(exception))
    {
      return false;
    }
  }
}
