namespace Kelvin.Server.Channels;

using Kelvin.Server.Application;
using Kelvin.Server.Models;

public interface IControlChannel : IChannelBase<ControlMessage> { }

public class ControlChannel(ILogger<ControlChannel> logger) : ChannelBase<ControlMessage>(logger), IControlChannel { }

public class ControlChannelRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddSingleton<IControlChannel, ControlChannel>();
  }
}
