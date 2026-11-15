using System.Buffers.Binary;
using System.Text;
using Shouldly;
using Xunit;

namespace Kelvin.Simulator.Tests;

public class GatewayProtocolTests
{
    private static readonly byte[] Mac = [2, 0xBB, 0, 0, 0, 0x10];

    [Fact]
    public void GatewayInfo_MatchesDiscoveryWireFormat()
    {
        Convert.ToHexString(GatewayProtocol.EncodeGatewayInfo()).ShouldBe("AB56021122334455");
    }

    [Fact]
    public void Uplink_EncodesAddressAndLittleEndianLength()
    {
        var frame = GatewayProtocol.EncodeUplink(Mac, new byte[256]);

        frame.Length.ShouldBe(266);
        frame[..2].ShouldBe(new byte[] { 0xAC, 0x57 });
        frame[2..8].ShouldBe(Mac);
        frame[8..10].ShouldBe(new byte[] { 0, 1 });
    }

    [Fact]
    public void SensorReading_PreservesTagPaddingAndFieldOffsets()
    {
        var sensor = SimulatedSensor.Create(0, 20f);
        sensor.TemperatureC = 22.5f;
        sensor.HumidityPercentage = 45f;
        sensor.CO2LevelPpm = 900;
        sensor.BatteryLevelPercentage = 75f;

        var frame = GatewayProtocol.EncodeSensorReading(sensor);

        frame.Length.ShouldBe(30);
        frame[2..8].ShouldBe(sensor.MacAddress);
        BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(8, 2)).ShouldBe((ushort)20);
        Encoding.ASCII.GetString(frame[10..14]).ShouldBe("KNOD");
        BinaryPrimitives.ReadSingleLittleEndian(frame.AsSpan(14, 4)).ShouldBe(22.5f);
        BinaryPrimitives.ReadSingleLittleEndian(frame.AsSpan(18, 4)).ShouldBe(45f);
        BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(22, 2)).ShouldBe((ushort)900);
        frame[24..26].ShouldBe(new byte[2]);
        BinaryPrimitives.ReadSingleLittleEndian(frame.AsSpan(26, 4)).ShouldBe(75f);
    }

    [Fact]
    public void HmiMessage_EncodesTagAndEnvelope()
    {
        var hmi = SimulatedHmi.Create(0, 20f);
        var frame = GatewayProtocol.EncodeHmiMessage(hmi, HmiMessageType.SetFanEnabled, [1, 2]);

        frame[2..8].ShouldBe(hmi.MacAddress);
        frame[10..14].ShouldBe(HmiProtocol.FrameTag);
        HmiProtocol
            .TryReadEnvelope(
                frame.AsSpan(14),
                out var type,
                out var index,
                out var count,
                out _,
                out var body
            )
            .ShouldBeTrue();
        type.ShouldBe(HmiMessageType.SetFanEnabled);
        index.ShouldBe((byte)0);
        count.ShouldBe((byte)1);
        body.ShouldBe(new byte[] { 1, 2 });
    }

    [Fact]
    public void Parser_AcceptsFragmentedHandshakeAndConsecutiveFrames()
    {
        var parser = new GatewayProtocol();
        parser.Append(" in"u8);
        parser.TryRead(out _).ShouldBeFalse();
        parser.Append("FO\r\n"u8);
        parser.TryRead(out var info).ShouldBeTrue();
        info.ShouldBeOfType<GatewayInfoRequest>();
        parser.Append([.. Downlink([1, 2]), .. Downlink([3])]);
        ReadPacket(parser).Payload.ShouldBe(new byte[] { 1, 2 });
        ReadPacket(parser).Payload.ShouldBe(new byte[] { 3 });
        parser.TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public void Parser_AcceptsEveryPossibleFrameSplit()
    {
        var frame = Downlink([1, 0xAD, 0x58, (byte)'\n', 5]);
        for (var split = 1; split < frame.Length; split++)
        {
            var parser = new GatewayProtocol();
            parser.Append(frame.AsSpan(0, split));
            parser.TryRead(out _).ShouldBeFalse();
            parser.Append(frame.AsSpan(split));
            var packet = ReadPacket(parser);
            packet.MacAddress.ShouldBe(Mac);
            packet.Payload.ShouldBe(frame[10..]);
            parser.TryRead(out _).ShouldBeFalse();
        }
    }

    [Fact]
    public void Parser_SkipsUnknownLinesAndResynchronizesAfterBadHeaders()
    {
        var parser = new GatewayProtocol();
        parser.Append([.. "unknown\n"u8, 0xAD, 0x57, 0x01, .. Downlink([42]), .. "info\n"u8]);

        ReadPacket(parser).Payload.ShouldBe(new byte[] { 42 });
        parser.TryRead(out var info).ShouldBeTrue();
        info.ShouldBeOfType<GatewayInfoRequest>();
        parser.TryRead(out _).ShouldBeFalse();
    }

    private static GatewayDownlink ReadPacket(GatewayProtocol parser)
    {
        parser.TryRead(out var input).ShouldBeTrue();
        return input.ShouldBeOfType<GatewayDownlink>();
    }

    private static byte[] Downlink(byte[] payload)
    {
        var frame = GatewayProtocol.EncodeUplink(Mac, payload);
        frame[0] = 0xAD;
        frame[1] = 0x58;
        return frame;
    }
}
