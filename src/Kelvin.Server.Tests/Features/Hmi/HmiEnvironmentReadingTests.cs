using System.Formats.Cbor;
using Kelvin.Server.Features.Hmi;
using Kelvin.Server.Models;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Panels;

public class HmiEnvironmentReadingTests
{
    [Fact]
    public void Encode_MatchesFirmwareGoldenVector()
    {
        var payload = HmiEnvironmentReadingEncoder.Encode(
            new EnvironmentReading
            {
                TemperatureC = 21.5f,
                HumidityPercentage = 50f,
                CO2LevelPpm = 400f,
            }
        );

        Convert.ToHexString(payload).ShouldBe("A4000101F94D6002F9524003F95E40");
        var reader = new CborReader(payload, CborConformanceMode.Strict);
        reader.ReadStartMap().ShouldBe(4);
        reader.ReadUInt32().ShouldBe(0u);
        reader.ReadUInt32().ShouldBe(1u);
        reader.ReadUInt32().ShouldBe(1u);
        reader.ReadSingle().ShouldBe(21.5f);
        reader.ReadUInt32().ShouldBe(2u);
        reader.ReadSingle().ShouldBe(50f);
        reader.ReadUInt32().ShouldBe(3u);
        reader.ReadSingle().ShouldBe(400f);
        reader.ReadEndMap();
        reader.BytesRemaining.ShouldBe(0);
    }

    [Theory]
    [InlineData(float.NaN, 50f, 400f)]
    [InlineData(21.5f, float.PositiveInfinity, 400f)]
    [InlineData(21.5f, 50f, float.NegativeInfinity)]
    public void Encode_NonFiniteReadingsAreRejected(float temperature, float humidity, float co2)
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            HmiEnvironmentReadingEncoder.Encode(
                new EnvironmentReading
                {
                    TemperatureC = temperature,
                    HumidityPercentage = humidity,
                    CO2LevelPpm = co2,
                }
            )
        );
    }

    [Fact]
    public void EncodedPayload_RoundTripsThroughUnchangedEnvelopeAndQueue()
    {
        var payload = HmiEnvironmentReadingEncoder.Encode(
            new EnvironmentReading { TemperatureC = -10.25f }
        );
        var channel = new Server.Channels.HmiOutboundChannel();
        channel.Write("AA:BB:CC:DD:EE:FF", HmiMessageType.EnvironmentReadingChanged, payload);

        channel.TryRead(out var message).ShouldBeTrue();
        HmiEnvelope
            .TryDecode(
                message.Payload[FrameTags.Size..],
                out var type,
                out _,
                out var count,
                out _,
                out var body
            )
            .ShouldBeTrue();

        type.ShouldBe(HmiMessageType.EnvironmentReadingChanged);
        count.ShouldBe((byte)1);
        body.ToArray().ShouldBe(payload);
        channel.TryRead(out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(21.5f, 50f, 400f)]
    [InlineData(-10.25f, 0f, 0f)]
    [InlineData(21.234567f, 49.12345f, 423.567f)]
    public void Simulator_DecodesActualServerPayload(float temperature, float humidity, float co2)
    {
        var payload = HmiEnvironmentReadingEncoder.Encode(
            new EnvironmentReading
            {
                TemperatureC = temperature,
                HumidityPercentage = humidity,
                CO2LevelPpm = co2,
            }
        );

        var reading = Simulator.HmiProtocol.DecodeEnvironmentReading(payload);

        reading.ShouldNotBeNull();
        reading.TemperatureC.ShouldBe(temperature);
        reading.HumidityPercentage.ShouldBe(humidity);
        reading.CO2LevelPpm.ShouldBe(co2);
    }

    [Theory]
    [InlineData("A503F95E401864820102000102F9524001F94D60")]
    [InlineData("A4000101FA41AC000002FA4248000003FA43C80000")]
    [InlineData("A4000101FB403580000000000002F9524003F95E40")]
    public void Simulator_AcceptsExtensionsReorderedFieldsAndAllFloatWidths(string hex)
    {
        var reading = Simulator.HmiProtocol.DecodeEnvironmentReading(Convert.FromHexString(hex));

        reading.ShouldNotBeNull();
        reading.TemperatureC.ShouldBe(21.5f);
        reading.HumidityPercentage.ShouldBe(50f);
        reading.CO2LevelPpm.ShouldBe(400f);
    }

    [Theory]
    [InlineData("")]
    [InlineData("000000000000000000000000")]
    [InlineData("A4000201F94D6002F9524003F95E40")]
    [InlineData("A4000101F94D6001F9524003F95E40")]
    [InlineData("A3000101F94D6002F95240")]
    [InlineData("A4000101F94D6002F9524003F95E4000")]
    [InlineData("A4000101F97C0002F9524003F95E40")]
    [InlineData("A4000101F97C0102F9524003F95E40")]
    [InlineData("A4000101617802F9524003F95E40")]
    [InlineData("A40001011502F9524003F95E40")]
    [InlineData("A4000101F94D6002F9524003F95E")]
    [InlineData("BF000101F94D6002F9524003F95E40FF")]
    [InlineData("A6000101F94D6002F9524003F95E401864F61864F6")]
    [InlineData("A5000101F94D6002F9524003F95E401864818181818100")]
    [InlineData("A5000101F94D6002F9524003F95E401864C000")]
    public void Simulator_RejectsInvalidMaps(string hex)
    {
        Simulator.HmiProtocol.DecodeEnvironmentReading(Convert.FromHexString(hex)).ShouldBeNull();
    }
}
