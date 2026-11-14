using System.Buffers.Binary;
using System.Text;

namespace Kelvin.Simulator;

internal abstract record GatewayInput;

internal sealed record GatewayInfoRequest : GatewayInput;

internal sealed record GatewayDownlink(byte[] MacAddress, byte[] Payload) : GatewayInput;

internal sealed class GatewayProtocol
{
    private const int MacLength = 6;
    private const int PrefixLength = 2 + MacLength + 2;
    private static readonly byte[] NodeFrameTag = [0x4B, 0x4E, 0x4F, 0x44];
    private static readonly byte[] GatewayMac = [0x02, 0x11, 0x22, 0x33, 0x44, 0x55];
    private readonly List<byte> receiveBuffer = [];

    public void Append(ReadOnlySpan<byte> bytes) => receiveBuffer.AddRange(bytes);

    public bool TryRead(out GatewayInput? input)
    {
        input = null;
        while (receiveBuffer.Count > 0)
        {
            if (receiveBuffer[0] == 0xAD)
            {
                if (receiveBuffer.Count < 2)
                    return false;
                if (receiveBuffer[1] != 0x58)
                {
                    receiveBuffer.RemoveAt(0);
                    continue;
                }

                if (receiveBuffer.Count < PrefixLength)
                    return false;
                var length = receiveBuffer[2 + MacLength] | (receiveBuffer[2 + MacLength + 1] << 8);
                if (receiveBuffer.Count < PrefixLength + length)
                    return false;

                input = new GatewayDownlink(
                    receiveBuffer.GetRange(2, MacLength).ToArray(),
                    receiveBuffer.GetRange(PrefixLength, length).ToArray()
                );
                receiveBuffer.RemoveRange(0, PrefixLength + length);
                return true;
            }

            var newlineIndex = receiveBuffer.IndexOf((byte)'\n');
            var downlinkIndex = receiveBuffer.IndexOf(0xAD);
            if (downlinkIndex > 0 && (newlineIndex < 0 || downlinkIndex < newlineIndex))
            {
                receiveBuffer.RemoveRange(0, downlinkIndex);
                continue;
            }
            if (newlineIndex < 0)
                return false;

            var line = Encoding
                .ASCII.GetString([.. receiveBuffer.GetRange(0, newlineIndex)])
                .Trim();
            receiveBuffer.RemoveRange(0, newlineIndex + 1);
            if (line.Equals("info", StringComparison.OrdinalIgnoreCase))
            {
                input = new GatewayInfoRequest();
                return true;
            }
        }
        return false;
    }

    public static byte[] EncodeUplink(byte[] macAddress, byte[] payload)
    {
        var frame = new byte[PrefixLength + payload.Length];
        frame[0] = 0xAC;
        frame[1] = 0x57;
        macAddress.CopyTo(frame, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(
            frame.AsSpan(2 + MacLength, 2),
            (ushort)payload.Length
        );
        payload.CopyTo(frame, PrefixLength);
        return frame;
    }

    public static byte[] EncodeSensorReading(SimulatedSensor sensor)
    {
        var payload = new byte[16];
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(0, 4), sensor.TemperatureC);
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(4, 4), sensor.HumidityPercentage);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(8, 2), sensor.CO2LevelPpm);
        BinaryPrimitives.WriteSingleLittleEndian(
            payload.AsSpan(12, 4),
            sensor.BatteryLevelPercentage
        );
        return EncodeUplink(sensor.MacAddress, [.. NodeFrameTag, .. payload]);
    }

    public static byte[] EncodeHmiMessage(SimulatedHmi hmi, HmiMessageType type, byte[] body) =>
        EncodeUplink(
            hmi.MacAddress,
            [.. HmiProtocol.FrameTag, .. HmiProtocol.Envelope(type, body)]
        );

    public static byte[] EncodeGatewayInfo() => [0xAB, 0x56, .. GatewayMac];
}
