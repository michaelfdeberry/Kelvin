using Kelvin.Server.Application;
using Kelvin.Server.Features.Hmi;

namespace Kelvin.Server.Messaging;

/// <summary>Forwards bus events to registered Hmi panels, reusing the existing broadcast handlers unchanged.</summary>
public class HmiBroadcastService(IDispatcher dispatcher)
  : IEventHandler<ControlStateChangedEvent>,
    IEventHandler<EnvironmentReadingChangedEvent>,
    IEventHandler<ThermostatConfigChangedEvent>,
    IEventHandler<PreferencesChangedEvent>,
    IEventHandler<SendNotificationEvent>,
    IEventHandler<DismissNotificationEvent>
{
  public Task HandleAsync(ControlStateChangedEvent @event, CancellationToken cancellationToken = default) =>
    dispatcher.DispatchAsync(new BroadcastHmiControlStateRequest(@event.Change), cancellationToken);

  public Task HandleAsync(EnvironmentReadingChangedEvent @event, CancellationToken cancellationToken = default) =>
    dispatcher.DispatchAsync(new BroadcastHmiEnvironmentReadingRequest(@event.Reading), cancellationToken);

  public Task HandleAsync(ThermostatConfigChangedEvent @event, CancellationToken cancellationToken = default) =>
    dispatcher.DispatchAsync(new BroadcastHmiStateRequest(), cancellationToken);

  public Task HandleAsync(PreferencesChangedEvent @event, CancellationToken cancellationToken = default) =>
    dispatcher.DispatchAsync(new BroadcastHmiPreferencesRequest(@event.Preferences), cancellationToken);

  public Task HandleAsync(SendNotificationEvent @event, CancellationToken cancellationToken = default) =>
    dispatcher.DispatchAsync(new BroadcastHmiNotificationRequest(@event.Notification), cancellationToken);

  public Task HandleAsync(DismissNotificationEvent @event, CancellationToken cancellationToken = default) =>
    dispatcher.DispatchAsync(new BroadcastHmiDismissNotificationRequest(@event.NotificationId), cancellationToken);
}

public class HmiBroadcastServiceRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IEventHandler<ControlStateChangedEvent>, HmiBroadcastService>();
    services.AddScoped<IEventHandler<EnvironmentReadingChangedEvent>, HmiBroadcastService>();
    services.AddScoped<IEventHandler<ThermostatConfigChangedEvent>, HmiBroadcastService>();
    services.AddScoped<IEventHandler<PreferencesChangedEvent>, HmiBroadcastService>();
    services.AddScoped<IEventHandler<SendNotificationEvent>, HmiBroadcastService>();
    services.AddScoped<IEventHandler<DismissNotificationEvent>, HmiBroadcastService>();
  }
}
