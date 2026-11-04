using Kelvin.Protocol;

namespace Kelvin.Simulator;

internal sealed class SimulatedHumidifier
{
    public static readonly byte[] FrameTag = [0x4B, 0x48, 0x55, 0x4D];
    public static readonly byte[] MacAddress = [0x02, 0x48, 0x55, 0x4D, 0x00, 0x01];
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromMinutes(2);

    private DateTimeOffset? lastPingAt;
    private DateTimeOffset? lastResponseAt;

    public bool Powered { get; private set; }
    public bool RelayOn { get; private set; }

    public byte[]? Tick(ControlStateSnapshot? control, DateTimeOffset now)
    {
        Powered = control is { ControlState: "Enable", CallState: "Heating" };
        if (!Powered)
        {
            RelayOn = false;
            lastPingAt = null;
            lastResponseAt = null;
            return null;
        }

        if (RelayOn && (lastResponseAt is null || now - lastResponseAt >= ResponseTimeout))
            RelayOn = false;

        if (lastPingAt is not null && now - lastPingAt < PingInterval)
            return null;

        lastPingAt = now;
        var body = HmiCbor.Encode(
            1,
            writer =>
            {
                writer.WriteUInt32(1);
                writer.WriteBoolean(RelayOn);
            }
        );
        return [.. FrameTag, 1, .. body];
    }

    public bool ReceiveState(byte[] payload, DateTimeOffset now)
    {
        if (
            !Powered
            || payload.Length <= FrameTag.Length
            || !payload.AsSpan(0, FrameTag.Length).SequenceEqual(FrameTag)
            || payload[FrameTag.Length] != 2
        )
            return false;

        try
        {
            var state = HmiCborMap.Parse(payload.AsMemory(FrameTag.Length + 1));
            RelayOn = state.Boolean(1);
            lastResponseAt = now;
            return true;
        }
        catch (Exception exception) when (HmiCbor.IsDecodeError(exception))
        {
            return false;
        }
    }
}
