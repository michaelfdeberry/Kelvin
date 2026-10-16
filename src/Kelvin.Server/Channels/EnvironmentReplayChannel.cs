namespace Kelvin.Server.Channels;

using Kelvin.Server.Application;
using Kelvin.Server.Models;

public interface IEnvironmentReplayChannel : IChannelBase<bool>;

public class EnvironmentReplayChannel(ILogger<EnvironmentReplayChannel> logger) : ChannelBase<bool>(logger), IEnvironmentReplayChannel { };

public class EnvironmentReplayChannelRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddSingleton<IEnvironmentReplayChannel, EnvironmentReplayChannel>();
  }
}
