using Kelvin.Server.Application;
using Kelvin.Server.Models;

namespace Kelvin.Server.Channels;

public interface ISensorPacketChannel : IChannelBase<SensorPacket>;

public class SensorPacketChannel(ILogger<SensorPacketChannel> logger) : ChannelBase<SensorPacket>(logger), ISensorPacketChannel { };

public class SensorPacketChannelRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddSingleton<ISensorPacketChannel, SensorPacketChannel>();
  }
}
