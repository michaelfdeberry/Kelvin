using Kelvin.Server.Application;
using Kelvin.Server.Channels;

namespace Kelvin.Server.Messaging;

public class EnvironmentBroadcastService(IEnvironmentReplayChannel environmentReplayChannel, IEnvironmentReadingsChannel environmentReadingChannel)
  : IEventHandler<ThermostatConfigChangedEvent>,
    IEventHandler<EnvironmentReadingChangedEvent>
{
  public Task HandleAsync(ThermostatConfigChangedEvent @event, CancellationToken cancellationToken = default)
  {
    return environmentReplayChannel.WriteAsync(true, cancellationToken);
  }

  public Task HandleAsync(EnvironmentReadingChangedEvent @event, CancellationToken cancellationToken = default)
  {
    return environmentReadingChannel.WriteAsync(@event.Reading, cancellationToken);
  }
}

public class EnvironmentReplayServiceRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IEventHandler<ThermostatConfigChangedEvent>, EnvironmentBroadcastService>();
    services.AddScoped<IEventHandler<EnvironmentReadingChangedEvent>, EnvironmentBroadcastService>();
  }
}
