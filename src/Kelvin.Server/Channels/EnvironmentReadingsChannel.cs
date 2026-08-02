namespace Kelvin.Server.Channels;

using Kelvin.Server.Application;
using Kelvin.Server.Models;

public interface IEnvironmentReadingsChannel : IChannelBase<EnvironmentReading>;

public class EnvironmentReadingsChannel(ILogger<EnvironmentReadingsChannel> logger)
  : ChannelBase<EnvironmentReading>(logger),
    IEnvironmentReadingsChannel { };

public class EnvironmentChannelRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddSingleton<IEnvironmentReadingsChannel, EnvironmentReadingsChannel>();
  }
}
