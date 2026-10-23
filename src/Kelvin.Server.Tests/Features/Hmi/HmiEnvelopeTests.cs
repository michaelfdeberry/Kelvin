using Kelvin.Server.Features.Hmi;
using Kelvin.Server.Models;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Panels;

public class HmiEnvelopeTests
{
    [Fact]
    public void ChunkedMessage_RoundTripsWithOneIdAndConsecutiveIndices()
    {
        var payload = Enumerable.Range(0, 600).Select(index => (byte)index).ToArray();
        var frames = HmiEnvelope.Encode(HmiMessageType.ThermostatStateChunk, payload);
        var bodies = new List<byte>();
        var messageId = BitConverter.ToUInt32(frames[0], 5);

        for (var index = 0; index < frames.Count; index++)
        {
            HmiEnvelope
                .TryDecode(
                    frames[index],
                    out var type,
                    out var chunkIndex,
                    out var chunkCount,
                    out var decodedId,
                    out var body
                )
                .ShouldBeTrue();
            type.ShouldBe(HmiMessageType.ThermostatStateChunk);
            chunkIndex.ShouldBe((byte)index);
            chunkCount.ShouldBe((byte)frames.Count);
            decodedId.ShouldBe(messageId);
            bodies.AddRange(body.ToArray());
        }

        bodies.ShouldBe(payload);
        BitConverter
            .ToUInt32(HmiEnvelope.Encode(HmiMessageType.ThermostatStateChunk, payload)[0], 5)
            .ShouldNotBe(messageId);
    }

    [Fact]
    public void FirmwareLayout_DecodesLittleEndianMessageId()
    {
        byte[] frame =
        [
            (byte)HmiMessageType.PreferencesChanged,
            0,
            1,
            2,
            0,
            0x78,
            0x56,
            0x34,
            0x12,
            1,
            0,
        ];

        HmiEnvelope
            .TryDecode(frame, out var type, out _, out _, out var messageId, out var body)
            .ShouldBeTrue();

        type.ShouldBe(HmiMessageType.PreferencesChanged);
        messageId.ShouldBe(0x12345678u);
        body.ToArray().ShouldBe(new byte[] { 1, 0 });
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(255, 1)]
    public void InvalidChunkMetadata_IsRejected(byte chunkIndex, byte chunkCount)
    {
        var frame = HmiEnvelope.Encode(HmiMessageType.PreferencesChanged, [0, 0]).Single();
        frame[1] = chunkIndex;
        frame[2] = chunkCount;

        HmiEnvelope.TryDecode(frame, out _, out _, out _, out _, out _).ShouldBeFalse();
    }

    [Fact]
    public void TruncatedTrailingAndLegacyFrames_AreRejected()
    {
        var frame = HmiEnvelope.Encode(HmiMessageType.PreferencesChanged, [0, 0]).Single();

        HmiEnvelope.TryDecode(frame[..^1], out _, out _, out _, out _, out _).ShouldBeFalse();
        HmiEnvelope.TryDecode([.. frame, 0], out _, out _, out _, out _, out _).ShouldBeFalse();
        HmiEnvelope
            .TryDecode(
                [(byte)HmiMessageType.PreferencesChanged, 0, 1, 2, 0, 0, 0],
                out _,
                out _,
                out _,
                out _,
                out _
            )
            .ShouldBeFalse();
    }

    [Fact]
    public void EmptyMessage_StillHasACompleteEnvelope()
    {
        var frame = HmiEnvelope.Encode(HmiMessageType.RequestState, []).Single();

        frame.Length.ShouldBe(HmiEnvelope.HeaderSize);
        HmiEnvelope
            .TryDecode(
                frame,
                out var type,
                out var chunkIndex,
                out var chunkCount,
                out _,
                out var body
            )
            .ShouldBeTrue();
        type.ShouldBe(HmiMessageType.RequestState);
        chunkIndex.ShouldBe((byte)0);
        chunkCount.ShouldBe((byte)1);
        body.IsEmpty.ShouldBeTrue();
    }
}
