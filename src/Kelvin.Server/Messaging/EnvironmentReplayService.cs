using Kelvin.Server.Application;
using Kelvin.Server.Channels;

namespace Kelvin.Server.Messaging;

/// <summary>
/// This service listens for thermostat configuration changes and triggers an environment replay signal to ensure the thermostat reacts promptly.
///
/// It's being added because if the user changes the set point the thermostat wouldn't react until the next environment reading is available,
/// and that could have taken up to 5 minutes for another environment reading to be available.
/// </summary>
public class EnvironmentReplayService(IEnvironmentReplayChannel environmentReplayChannel) : IEventHandler<ThermostatConfigChangedEvent>
{
  public Task HandleAsync(ThermostatConfigChangedEvent @event, CancellationToken cancellationToken = default)
  {
    return environmentReplayChannel.WriteAsync(true, cancellationToken);
  }
}

public class EnvironmentReplayServiceRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IEventHandler<ThermostatConfigChangedEvent>, EnvironmentReplayService>();
  }
}
