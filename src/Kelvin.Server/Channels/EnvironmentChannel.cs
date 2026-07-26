namespace Kelvin.Server.Channels;

using Kelvin.Server.Application;
using Kelvin.Server.Models;

public interface IEnvironmentChannel : IChannelBase<Environment>;

public class EnvironmentChannel(ILogger<EnvironmentChannel> logger) : ChannelBase<Environment>(logger), IEnvironmentChannel { };

public class EnvironmentChannelRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddSingleton<IEnvironmentChannel, EnvironmentChannel>();
  }
}
