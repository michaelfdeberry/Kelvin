using Kelvin.Protocol;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Hmi;
using Kelvin.Server.Models;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Channels;

public class DeviceOutboundChannelTests
{
    private const string PanelMac = "AA:BB:CC:DD:EE:FF";

    [Fact]
    public void NotificationsAndDismissals_RetainEveryMessageInOrder()
    {
        var channel = new DeviceOutboundChannel();
        channel.WriteHmi(PanelMac, HmiMessageType.Notify, [1]);
        channel.WriteHmi(PanelMac, HmiMessageType.Notify, [2]);
        channel.WriteHmi(PanelMac, HmiMessageType.DismissNotification, [3]);
        channel.WriteHmi(PanelMac, HmiMessageType.DismissNotification, [4]);
        channel.WriteHmi(PanelMac, HmiMessageType.EnvironmentReadingChanged, [5]);
        channel.WriteHmi(PanelMac, HmiMessageType.EnvironmentReadingChanged, [6]);
        Drain(channel).SelectMany(Body).ShouldBe(new byte[] { 1, 2, 3, 4, 6 });
    }

    [Fact]
    public void RepeatedSnapshots_RetainOnlyLatestPerPanelAndType()
    {
        var channel = new DeviceOutboundChannel();
        for (var index = 0; index < 10000; index++)
            channel.WriteHmi(
                PanelMac,
                HmiMessageType.EnvironmentReadingChanged,
                BitConverter.GetBytes(index)
            );
        channel.WriteHmi(PanelMac, HmiMessageType.ControlStateChanged, [42]);
        channel.WriteHmi("11:22:33:44:55:66", HmiMessageType.EnvironmentReadingChanged, [43]);

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
    public void SameMessageTypeOnDifferentFrameTags_IsNotCoalescedAndKeepsItsTag()
    {
        var channel = new DeviceOutboundChannel();
        channel.Write(PanelMac, FrameTags.Humidifier, 2, [1]);
        channel.WriteHmi(PanelMac, (HmiMessageType)2, [2]);
        channel.Write(PanelMac, FrameTags.Humidifier, 2, [3]);

        var messages = Drain(channel);

        messages.Count.ShouldBe(2);
        messages[0].Payload.Take(FrameTags.Size).ShouldBe(FrameTags.Hmi);
        Body(messages[0]).ShouldBe(new byte[] { 2 });
        messages[1].Payload.Take(FrameTags.Size).ShouldBe(FrameTags.Humidifier);
        Body(messages[1]).ShouldBe(new byte[] { 3 });
    }

    [Fact]
    public void PendingChunkedSnapshot_IsReplacedAsAWhole()
    {
        var channel = new DeviceOutboundChannel();
        var latest = Enumerable.Repeat((byte)2, 600).ToArray();
        channel.WriteHmi(PanelMac, HmiMessageType.ThermostatStateChunk, new byte[400]);
        channel.WriteHmi(PanelMac.ToLowerInvariant(), HmiMessageType.ThermostatStateChunk, latest);

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
        var channel = new DeviceOutboundChannel();
        var original = Enumerable.Repeat((byte)1, 600).ToArray();
        var latest = Enumerable.Repeat((byte)3, 400).ToArray();
        channel.WriteHmi(PanelMac, HmiMessageType.ThermostatStateChunk, original);
        channel.TryRead(out var first).ShouldBeTrue();
        channel.WriteHmi(PanelMac, HmiMessageType.ThermostatStateChunk, new byte[500]);
        channel.WriteHmi(PanelMac, HmiMessageType.ThermostatStateChunk, latest);

        var remaining = Drain(channel);

        remaining.Count.ShouldBe(6);
        new[] { first }.Concat(remaining.Take(3)).SelectMany(Body).ShouldBe(original);
        remaining.Skip(3).SelectMany(Body).ShouldBe(latest);
    }

    [Fact]
    public void MessageLimit_EvictsOldestWholeSnapshotWithoutDroppingActiveChunks()
    {
        var channel = new DeviceOutboundChannel();
        channel.WriteHmi(PanelMac, HmiMessageType.ThermostatStateChunk, new byte[400]);
        channel.TryRead(out _).ShouldBeTrue();
        for (var index = 0; index <= DeviceOutboundChannel.MaxPendingMessages; index++)
            channel.WriteHmi(index.ToString(), HmiMessageType.EnvironmentReadingChanged, [42]);

        var messages = Drain(channel);

        messages.Count.ShouldBe(DeviceOutboundChannel.MaxPendingMessages + 2);
        messages.Take(2).ShouldAllBe(message => message.MacAddress == PanelMac);
        messages[2].MacAddress.ShouldBe("1");
        messages.Last().MacAddress.ShouldBe(DeviceOutboundChannel.MaxPendingMessages.ToString());
    }

    [Fact]
    public void PayloadLimit_EvictsWholeChunkedSnapshots()
    {
        var channel = new DeviceOutboundChannel();
        var retainedCount =
            DeviceOutboundChannel.MaxPendingPayloadBytes / DeviceEnvelope.MaxPayloadSize;
        for (var index = 0; index <= retainedCount; index++)
            channel.WriteHmi(
                index.ToString(),
                HmiMessageType.ThermostatStateChunk,
                new byte[DeviceEnvelope.MaxPayloadSize]
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
        var channel = new DeviceOutboundChannel();
        byte[] payload = [42];
        channel.WriteHmi(PanelMac, HmiMessageType.EnvironmentReadingChanged, payload);
        payload[0] = 99;

        Should.Throw<ArgumentOutOfRangeException>(() =>
            channel.WriteHmi(
                PanelMac,
                HmiMessageType.EnvironmentReadingChanged,
                new byte[DeviceEnvelope.MaxPayloadSize + 1]
            )
        );

        Body(Drain(channel).Single()).ShouldBe(new byte[] { 42 });
    }

    [Fact]
    public void ConcurrentWriters_DoNotInterleaveChunkedSnapshots()
    {
        var channel = new DeviceOutboundChannel();
        Parallel.For(
            0,
            1000,
            index =>
                channel.WriteHmi(
                    PanelMac,
                    HmiMessageType.ThermostatStateChunk,
                    Enumerable.Repeat((byte)index, 600).ToArray()
                )
        );
        var latest = Enumerable.Repeat((byte)42, 600).ToArray();
        channel.WriteHmi(PanelMac, HmiMessageType.ThermostatStateChunk, latest);

        Drain(channel).SelectMany(Body).ShouldBe(latest);
    }

    private static byte[] Body(DeviceOutboundFrame message) =>
        message.Payload[(FrameTags.Size + DeviceEnvelope.HeaderSize)..];

    private static List<DeviceOutboundFrame> Drain(DeviceOutboundChannel channel)
    {
        var messages = new List<DeviceOutboundFrame>();
        while (channel.TryRead(out var message))
            messages.Add(message);
        return messages;
    }
}
