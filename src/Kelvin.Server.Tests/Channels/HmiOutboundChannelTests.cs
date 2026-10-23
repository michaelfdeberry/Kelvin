using Kelvin.Server.Channels;
using Kelvin.Server.Features.Hmi;
using Kelvin.Server.Models;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Channels;

public class HmiOutboundChannelTests
{
    private const string PanelMac = "AA:BB:CC:DD:EE:FF";

    [Fact]
    public void RepeatedSnapshots_RetainOnlyLatestPerPanelAndType()
    {
        var channel = new HmiOutboundChannel();
        for (var index = 0; index < 10000; index++)
            channel.Write(
                PanelMac,
                HmiMessageType.EnvironmentReadingChanged,
                BitConverter.GetBytes(index)
            );
        channel.Write(PanelMac, HmiMessageType.ControlStateChanged, [42]);
        channel.Write("11:22:33:44:55:66", HmiMessageType.EnvironmentReadingChanged, [43]);

        var messages = Drain(channel);

        messages.Count.ShouldBe(3);
        Body(messages[0]).ShouldBe(BitConverter.GetBytes(9999));
        Body(messages[1]).ShouldBe(new byte[] { 42 });
        messages[2].MacAddress.ShouldBe("11:22:33:44:55:66");
        messages.ShouldAllBe(message =>
            message.Payload.Take(FrameTags.Size).SequenceEqual(FrameTags.Hmi)
        );
    }

    [Fact]
    public void PendingChunkedSnapshot_IsReplacedAsAWhole()
    {
        var channel = new HmiOutboundChannel();
        var latest = Enumerable.Repeat((byte)2, 600).ToArray();
        channel.Write(PanelMac, HmiMessageType.ThermostatStateChunk, new byte[400]);
        channel.Write(PanelMac.ToLowerInvariant(), HmiMessageType.ThermostatStateChunk, latest);

        var messages = Drain(channel);

        messages.Count.ShouldBe(4);
        messages.SelectMany(Body).ShouldBe(latest);
        messages
            .Select(message => message.Payload[FrameTags.Size + 1])
            .ShouldBe(new byte[] { 0, 1, 2, 3 });
        messages.ShouldAllBe(message => message.Payload[FrameTags.Size + 2] == 4);
    }

    [Fact]
    public void ActiveChunkedSnapshot_FinishesBeforeLatestPendingReplacement()
    {
        var channel = new HmiOutboundChannel();
        var original = Enumerable.Repeat((byte)1, 600).ToArray();
        var latest = Enumerable.Repeat((byte)3, 400).ToArray();
        channel.Write(PanelMac, HmiMessageType.ThermostatStateChunk, original);
        channel.TryRead(out var first).ShouldBeTrue();
        channel.Write(PanelMac, HmiMessageType.ThermostatStateChunk, new byte[500]);
        channel.Write(PanelMac, HmiMessageType.ThermostatStateChunk, latest);

        var remaining = Drain(channel);

        remaining.Count.ShouldBe(6);
        new[] { first }.Concat(remaining.Take(3)).SelectMany(Body).ShouldBe(original);
        remaining.Skip(3).SelectMany(Body).ShouldBe(latest);
    }

    [Fact]
    public void MessageLimit_EvictsOldestWholeSnapshotWithoutDroppingActiveChunks()
    {
        var channel = new HmiOutboundChannel();
        channel.Write(PanelMac, HmiMessageType.ThermostatStateChunk, new byte[400]);
        channel.TryRead(out _).ShouldBeTrue();
        for (var index = 0; index <= HmiOutboundChannel.MaxPendingMessages; index++)
            channel.Write(index.ToString(), HmiMessageType.EnvironmentReadingChanged, [42]);

        var messages = Drain(channel);

        messages.Count.ShouldBe(HmiOutboundChannel.MaxPendingMessages + 2);
        messages.Take(2).ShouldAllBe(message => message.MacAddress == PanelMac);
        messages[2].MacAddress.ShouldBe("1");
        messages.Last().MacAddress.ShouldBe(HmiOutboundChannel.MaxPendingMessages.ToString());
    }

    [Fact]
    public void PayloadLimit_EvictsWholeChunkedSnapshots()
    {
        var channel = new HmiOutboundChannel();
        var retainedCount = HmiOutboundChannel.MaxPendingPayloadBytes / HmiEnvelope.MaxPayloadSize;
        for (var index = 0; index <= retainedCount; index++)
            channel.Write(
                index.ToString(),
                HmiMessageType.ThermostatStateChunk,
                new byte[HmiEnvelope.MaxPayloadSize]
            );

        var messages = Drain(channel);

        messages.Count.ShouldBe(retainedCount * byte.MaxValue);
        messages.First().MacAddress.ShouldBe("1");
        foreach (var group in messages.GroupBy(message => message.MacAddress))
        {
            group.Count().ShouldBe(byte.MaxValue);
            group
                .Select(message => message.Payload[FrameTags.Size + 1])
                .ShouldBe(Enumerable.Range(0, byte.MaxValue).Select(index => (byte)index));
        }
    }

    [Fact]
    public void Write_CopiesPayloadAndRejectsUnrepresentableMessages()
    {
        var channel = new HmiOutboundChannel();
        byte[] payload = [42];
        channel.Write(PanelMac, HmiMessageType.EnvironmentReadingChanged, payload);
        payload[0] = 99;

        Should.Throw<ArgumentOutOfRangeException>(() =>
            channel.Write(
                PanelMac,
                HmiMessageType.EnvironmentReadingChanged,
                new byte[HmiEnvelope.MaxPayloadSize + 1]
            )
        );

        Body(Drain(channel).Single()).ShouldBe(new byte[] { 42 });
    }

    [Fact]
    public void ConcurrentWriters_DoNotInterleaveChunkedSnapshots()
    {
        var channel = new HmiOutboundChannel();
        Parallel.For(
            0,
            1000,
            index =>
                channel.Write(
                    PanelMac,
                    HmiMessageType.ThermostatStateChunk,
                    Enumerable.Repeat((byte)index, 600).ToArray()
                )
        );
        var latest = Enumerable.Repeat((byte)42, 600).ToArray();
        channel.Write(PanelMac, HmiMessageType.ThermostatStateChunk, latest);

        Drain(channel).SelectMany(Body).ShouldBe(latest);
    }

    private static byte[] Body(HmiOutboundMessage message) =>
        message.Payload[(FrameTags.Size + HmiEnvelope.HeaderSize)..];

    private static List<HmiOutboundMessage> Drain(HmiOutboundChannel channel)
    {
        var messages = new List<HmiOutboundMessage>();
        while (channel.TryRead(out var message))
            messages.Add(message);
        return messages;
    }
}
