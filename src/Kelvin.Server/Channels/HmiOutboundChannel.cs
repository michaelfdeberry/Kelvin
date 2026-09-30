using System.Threading.Channels;
using Kelvin.Server.Application;
using Kelvin.Server.Models;

namespace Kelvin.Server.Channels;

public record HmiOutboundMessage(string MacAddress, byte[] Payload);

/// <summary>
/// A plain single-consumer work queue (not the pub/sub ChannelBase pattern) - GatewayService is the only
/// reader and drains it into serial downlink frames addressed to a specific Hmi's MAC.
/// </summary>
public interface IHmiOutboundChannel
{
  void Write(string macAddress, byte[] payload);
  bool TryRead(out HmiOutboundMessage message);
}

public class HmiOutboundChannel : IHmiOutboundChannel
{
  private readonly Channel<HmiOutboundMessage> _channel = Channel.CreateUnbounded<HmiOutboundMessage>();

  // The Gateway relays outbound frames verbatim (it doesn't know or care about tags), so the tag identifying
  // this as an Hmi frame is prepended here, once, rather than by every caller that writes to this channel.
  public void Write(string macAddress, byte[] payload)
  {
    var frame = new byte[FrameTags.Size + payload.Length];
    Buffer.BlockCopy(FrameTags.Hmi, 0, frame, 0, FrameTags.Size);
    Buffer.BlockCopy(payload, 0, frame, FrameTags.Size, payload.Length);

    _channel.Writer.TryWrite(new HmiOutboundMessage(macAddress, frame));
  }

  public bool TryRead(out HmiOutboundMessage message) => _channel.Reader.TryRead(out message!);
}

public class HmiOutboundChannelRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddSingleton<IHmiOutboundChannel, HmiOutboundChannel>();
  }
}
