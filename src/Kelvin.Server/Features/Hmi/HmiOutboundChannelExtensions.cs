using Kelvin.Server.Channels;
using Kelvin.Server.Models;

namespace Kelvin.Server.Features.Hmi;

public static class HmiOutboundChannelExtensions
{
  /// <summary>Queues an Hmi message; notifications and dismissals are events, so every one is kept in order.</summary>
  public static void WriteHmi(this IDeviceOutboundChannel channel, string macAddress, HmiMessageType messageType, byte[] payload) =>
    channel.Write(
      macAddress,
      FrameTags.Hmi,
      (byte)messageType,
      payload,
      coalesce: messageType is not HmiMessageType.Notify and not HmiMessageType.DismissNotification
    );
}
