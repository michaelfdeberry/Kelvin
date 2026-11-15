using Kelvin.Server.Application;
using Kelvin.Server.Features.Control;
using Kelvin.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Kelvin.Server.Messaging;

/// <summary>Forwards bus events to every connected browser client over the single realtime hub.</summary>
public class SignalRBroadcastService(IHubContext<RealtimeHub, IRealtimeClient> hub)
  : IEventHandler<ControlStateChangedEvent>,
    IEventHandler<EnvironmentReadingChangedEvent>,
    IEventHandler<ThermostatConfigChangedEvent>,
    IEventHandler<SensorsChangedEvent>,
    IEventHandler<DevicesChangedEvent>,
    IEventHandler<DeviceStateChangedEvent>,
    IEventHandler<SendNotificationEvent>,
    IEventHandler<DismissNotificationEvent>,
    IEventHandler<PreferencesChangedEvent>
{
  public Task HandleAsync(ControlStateChangedEvent @event, CancellationToken cancellationToken = default) =>
    hub.Clients.All.Receive(new RealtimeMessage(RealtimeMessageTypes.ControlStateChanged, ControlStateChangeDto.FromEntity(@event.Change)));

  public Task HandleAsync(EnvironmentReadingChangedEvent @event, CancellationToken cancellationToken = default) =>
    hub.Clients.All.Receive(new RealtimeMessage(RealtimeMessageTypes.EnvironmentReadingChanged, @event.Reading));

  public Task HandleAsync(ThermostatConfigChangedEvent @event, CancellationToken cancellationToken = default) =>
    hub.Clients.All.Receive(new RealtimeMessage(RealtimeMessageTypes.ThermostatSettingsChanged, null));

  public Task HandleAsync(SensorsChangedEvent @event, CancellationToken cancellationToken = default) =>
    hub.Clients.All.Receive(new RealtimeMessage(RealtimeMessageTypes.SensorsSettingsChanged, null));

  public Task HandleAsync(DevicesChangedEvent @event, CancellationToken cancellationToken = default) =>
    hub.Clients.All.Receive(new RealtimeMessage(RealtimeMessageTypes.DevicesSettingsChanged, null));

  public Task HandleAsync(DeviceStateChangedEvent @event, CancellationToken cancellationToken = default) =>
    hub.Clients.All.Receive(new RealtimeMessage(RealtimeMessageTypes.DeviceStateChanged, @event.States));

  public Task HandleAsync(SendNotificationEvent @event, CancellationToken cancellationToken = default) =>
    hub.Clients.All.Receive(new RealtimeMessage(RealtimeMessageTypes.NotificationSent, @event.Notification));

  public Task HandleAsync(DismissNotificationEvent @event, CancellationToken cancellationToken = default) =>
    hub.Clients.All.Receive(new RealtimeMessage(RealtimeMessageTypes.NotificationDismissed, @event.NotificationId));

  public Task HandleAsync(PreferencesChangedEvent @event, CancellationToken cancellationToken = default) =>
    hub.Clients.All.Receive(new RealtimeMessage(RealtimeMessageTypes.PreferencesChanged, @event.Preferences));
}

public class SignalRBroadcastServiceRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IEventHandler<ControlStateChangedEvent>, SignalRBroadcastService>();
    services.AddScoped<IEventHandler<EnvironmentReadingChangedEvent>, SignalRBroadcastService>();
    services.AddScoped<IEventHandler<ThermostatConfigChangedEvent>, SignalRBroadcastService>();
    services.AddScoped<IEventHandler<SensorsChangedEvent>, SignalRBroadcastService>();
    services.AddScoped<IEventHandler<DevicesChangedEvent>, SignalRBroadcastService>();
    services.AddScoped<IEventHandler<DeviceStateChangedEvent>, SignalRBroadcastService>();
    services.AddScoped<IEventHandler<SendNotificationEvent>, SignalRBroadcastService>();
    services.AddScoped<IEventHandler<DismissNotificationEvent>, SignalRBroadcastService>();
    services.AddScoped<IEventHandler<PreferencesChangedEvent>, SignalRBroadcastService>();
  }
}
