using Kelvin.Protocol;

namespace Kelvin.Simulator;

// Mirrors Kelvin.Server's Features/Humidifiers/HumidifierMessageType ordinals - keep in sync.
internal enum HumidifierMessageType : byte
{
    CheckIn = 1,
    State = 2,
    StateAck = 3,
}

/// <summary>
/// Mirrors the humidifier firmware: always powered, checks in at boot and every 5 minutes, applies and
/// acknowledges each pushed state, and releases the relay when the pushes stop.
/// </summary>
internal sealed class SimulatedHumidifier
{
    public static readonly byte[] FrameTag = [0x4B, 0x48, 0x55, 0x4D];
    public static readonly byte[] MacAddress = [0x02, 0x48, 0x55, 0x4D, 0x00, 0x01];
    private static readonly TimeSpan CheckInInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StateTimeout = TimeSpan.FromMinutes(2);

    private DateTimeOffset? lastCheckInAt;
    private DateTimeOffset? lastStateAt;

    public bool RelayOn { get; private set; }

    /// <summary>Returns a CheckIn frame when one is due, otherwise null.</summary>
    public byte[]? Tick(DateTimeOffset now)
    {
        if (RelayOn && (lastStateAt is null || now - lastStateAt >= StateTimeout))
            RelayOn = false;

        if (lastCheckInAt is not null && now - lastCheckInAt < CheckInInterval)
            return null;

        lastCheckInAt = now;
        return Encode(HumidifierMessageType.CheckIn);
    }

    /// <summary>Applies a State frame and returns its StateAck, or null if the frame is invalid.</summary>
    public byte[]? ReceiveState(byte[] payload, DateTimeOffset now)
    {
        if (
            payload.Length <= FrameTag.Length
            || !payload.AsSpan(0, FrameTag.Length).SequenceEqual(FrameTag)
            || !DeviceEnvelope.TryDecode(
                payload.AsSpan(FrameTag.Length),
                out var type,
                out _,
                out var chunkCount,
                out _,
                out var body
            )
            || type != (byte)HumidifierMessageType.State
            || chunkCount != 1
        )
            return null;

        try
        {
            RelayOn = HmiCborMap.Parse(body.ToArray()).Boolean(1);
        }
        catch (Exception exception) when (HmiCbor.IsDecodeError(exception))
        {
            return null;
        }

        lastStateAt = now;
        return Encode(HumidifierMessageType.StateAck);
    }

    private byte[] Encode(HumidifierMessageType type)
    {
        var body = HmiCbor.Encode(
            1,
            writer =>
            {
                writer.WriteUInt32(1);
                writer.WriteBoolean(RelayOn);
            }
        );
        return [.. FrameTag, .. DeviceEnvelope.Encode((byte)type, body).Single()];
    }
}
