using System.Reflection;
using System.Text;
using FakeItEasy;
using Kelvin.Protocol;
using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Hmi;
using Kelvin.Server.Features.Notifications;
using Kelvin.Server.Features.Preferences;
using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;
using Kelvin.Server.Services;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Panels;

public class HmiNotificationTests
{
    private const string Mac = "aa:bb:cc:dd:ee:ff";

    [Fact]
    public void NotificationAndDismissal_MatchFirmwareGoldenVectors()
    {
        var notification = new Notification
        {
            Id = new Guid("03020100-0504-0706-0809-0a0b0c0d0e0f"),
            Message = "Failed",
            Type = NotificationType.Error,
            Dismissible = true,
            Heading = "Settings",
            Banner = false,
            Duration = 10000,
        };
        Convert
            .ToHexString(HmiNotificationEncoder.Encode(notification, false))
            .ShouldBe(
                "A900010150000102030405060708090A0B0C0D0E0F02664661696C6564030304F5056853657474696E677306F40719271008F4"
            );
        Convert
            .ToHexString(HmiNotificationEncoder.EncodeDismiss(notification.Id))
            .ShouldBe("A200010150000102030405060708090A0B0C0D0E0F");
        var map = HmiCborMap.Parse(
            HmiNotificationEncoder.Encode(new Notification { Message = "Info" })
        );
        map.Boolean(8).ShouldBeTrue();
        var decoded = Simulator.HmiProtocol.DecodeNotification(
            HmiNotificationEncoder.Encode(notification, false)
        );
        decoded.ShouldNotBeNull();
        decoded.Id.ShouldBe(notification.Id);
        decoded.Message.ShouldBe(notification.Message);
        decoded.Heading.ShouldBe(notification.Heading);
        decoded.Duration.ShouldBe(10000u);
        decoded.Persistent.ShouldBeFalse();
        Simulator
            .HmiProtocol.DecodeNotificationDismissal(
                HmiNotificationEncoder.EncodeDismiss(notification.Id)
            )!
            .Id.ShouldBe(notification.Id);
    }

    [Fact]
    public void LongUnicodeText_IsBoundedWithoutSplittingCharacters()
    {
        var body = HmiNotificationEncoder.Encode(
            new Notification
            {
                Message = string.Concat(Enumerable.Repeat("\u20ac", 1000)),
                Heading = new string('x', 200),
            }
        );
        var reader = new System.Formats.Cbor.CborReader(body);
        reader.ReadStartMap();
        reader.ReadUInt32();
        reader.ReadUInt32();
        reader.ReadUInt32();
        reader.ReadByteString();
        reader.ReadUInt32();
        var text = reader.ReadTextString();
        Encoding.UTF8.GetByteCount(text).ShouldBeLessThanOrEqualTo(1024);
        text.ShouldNotContain("\ufffd");
        body.Length.ShouldBeLessThan(2048);
    }

    [Fact]
    public async Task Broadcast_OnlyTargetsEnabledNonDeletedPanelsForNotifyAndDismiss()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        context.Hmis.AddRange(
            new Models.Hmi { MacAddress = Mac, Enabled = true },
            new Models.Hmi { MacAddress = "disabled", Enabled = false },
            new Models.Hmi
            {
                MacAddress = "deleted",
                Enabled = true,
                DeletedAt = DateTimeOffset.UtcNow,
            }
        );
        await context.SaveChangesAsync();
        var channel = new HmiOutboundChannel();
        var handler = new BroadcastHmiNotificationHandler(context, channel);
        var notification = new Notification { Message = "Test" };
        await handler.HandleAsync(new BroadcastHmiNotificationRequest(notification));
        await handler.HandleAsync(new BroadcastHmiDismissNotificationRequest(notification.Id));
        var types = new List<HmiMessageType>();
        while (channel.TryRead(out var frame))
        {
            frame.MacAddress.ShouldBe(Mac);
            types.Add((HmiMessageType)frame.Payload[FrameTags.Size]);
        }
        types.ShouldBe(new[] { HmiMessageType.Notify, HmiMessageType.DismissNotification });
    }

    [Fact]
    public async Task PanelDismiss_UsesStandardHandlerWithoutRequiringThermostat()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var dispatcher = A.Fake<IDispatcher>();
        var notification = new Notification { Message = "Test", Dismissible = true };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        var handler = new ReceiveHmiCommandHandler(
            context,
            A.Fake<IHandler<UpdateThermostatRequest>>(),
            A.Fake<IHandler<UpdateThermostatSettingsRequest>>(),
            A.Fake<IHandler<SaveSensorPacketRequest>>(),
            A.Fake<IHandler<SendHmiStateRequest>>(),
            new DismissNotificationHandler(context, dispatcher)
        );
        var frame = HmiEnvelope
            .Encode(
                HmiMessageType.DismissNotification,
                HmiNotificationEncoder.EncodeDismiss(notification.Id)
            )
            .Single();
        var result = await handler.HandleAsync(new ReceiveHmiCommandRequest(Mac, frame));
        result.IsSuccess.ShouldBeTrue();
        notification.DeletedAt.ShouldNotBeNull();
        A.CallTo(() =>
                dispatcher.PublishAsync(
                    A<DismissNotificationEvent>.That.Matches(value =>
                        value.NotificationId == notification.Id
                    ),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GatewayFailure_NotifiesOnlyOriginatingPanelWithoutPublishing(bool failure)
    {
        var dispatcher = A.Fake<IDispatcher>();
        var outbound = new HmiOutboundChannel();
        A.CallTo(() =>
                dispatcher.DispatchAsync(A<ReceiveHmiCommandRequest>._, A<CancellationToken>._)
            )
            .Returns(failure ? Result.Failure(new Error("Test", "Rejected")) : Result.Success());
        var service = new GatewayService(NullLogger<GatewayService>.Instance, dispatcher, outbound);
        var method = typeof(GatewayService).GetMethod(
            "HandleHmiCommandAsync",
            BindingFlags.NonPublic | BindingFlags.Instance
        )!;
        await (Task)
            method.Invoke(service, new object[] { Mac, new byte[] { 1 }, CancellationToken.None })!;
        outbound.TryRead(out var frame).ShouldBe(failure);
        if (failure)
        {
            frame.MacAddress.ShouldBe(Mac);
            frame.Payload[FrameTags.Size].ShouldBe((byte)HmiMessageType.Notify);
            var map = HmiCborMap.Parse(frame.Payload[(FrameTags.Size + HmiEnvelope.HeaderSize)..]);
            map.Boolean(8).ShouldBeFalse();
            map.UInt(3, 3).ShouldBe((uint)NotificationType.Error);
        }
        A.CallTo(dispatcher)
            .Where(call => call.Method.Name == "PublishAsync")
            .MustNotHaveHappened();
        outbound.TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task WakeSnapshot_ReplaysActiveNotificationsButNotDismissedOnes()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        context.Notifications.AddRange(
            new Notification { Message = "Active" },
            new Notification { Message = "Banner", Banner = true },
            new Notification { Message = "Dismissed", DeletedAt = DateTimeOffset.UtcNow }
        );
        await context.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var channel = new HmiOutboundChannel();
        var handler = new SendHmiStateHandler(
            context,
            channel,
            NullLogger<SendHmiStateHandler>.Instance,
            new GetThermostatHandler(context, cache),
            new GetPreferencesHandler(context)
        );
        (await handler.HandleAsync(new SendHmiStateRequest(Mac))).IsSuccess.ShouldBeTrue();
        var count = 0;
        while (channel.TryRead(out var frame))
        {
            frame.MacAddress.ShouldBe(Mac);
            if (frame.Payload[FrameTags.Size] == (byte)HmiMessageType.Notify)
                count++;
        }
        count.ShouldBe(2);
    }
}
