namespace Kelvin.Server.Models;

/// <summary>
/// The 4-byte tag every device prepends to its own ESP-NOW frames (see Kelvin.Devices/Common/SensorPayload.h
/// for Node, and the future Hmi firmware). The Gateway relays these raw without inspecting them, so adding a
/// new device type only requires a new tag here plus a case in GatewayService - no Gateway.ino change.
/// </summary>
public static class FrameTags
{
  public const int Size = 4;

  public static readonly byte[] Node = [0x4B, 0x4E, 0x4F, 0x44]; // "KNOD"

  public static readonly byte[] Hmi = [0x4B, 0x48, 0x4D, 0x49]; // "KHMI"
}
