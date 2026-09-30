using System.Threading.Channels;
using Kelvin.Server.Application;

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

  public void Write(string macAddress, byte[] payload) => _channel.Writer.TryWrite(new HmiOutboundMessage(macAddress, payload));

  public bool TryRead(out HmiOutboundMessage message) => _channel.Reader.TryRead(out message!);
}

public class HmiOutboundChannelRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddSingleton<IHmiOutboundChannel, HmiOutboundChannel>();
  }
}
