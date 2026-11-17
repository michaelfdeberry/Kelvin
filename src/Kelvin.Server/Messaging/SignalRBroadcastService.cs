using Kelvin.Server.Application;
using Kelvin.Server.Features.Control;
using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Kelvin.Server.Messaging;

/// <summary>Forwards bus events to every connected browser client over the single realtime hub.</summary>
public class SignalRBroadcastService(IHubContext<RealtimeHub, IRealtimeClient> hub)
  : IEventHandler<ControlStateChangedEvent>,
    IEventHandler<EnvironmentReadingChangedEvent>,
    IEventHandler<ThermostatConfigChangedEvent>,
    IEventHandler<SensorChangedEvent>,
    IEventHandler<SensorDeletedEvent>,
    IEventHandler<DeviceChangedEvent>,
    IEventHandler<DeviceDeletedEvent>,
    IEventHandler<DeviceStateChangedEvent>,
    IEventHandler<RelayStatesChangedEvent>,
    IEventHandler<WeatherForecastChangedEvent>,
    IEventHandler<SendNotificationEvent>,
    IEventHandler<DismissNotificationEvent>,
    IEventHandler<PreferencesChangedEvent>
{
  public Task HandleAsync(ControlStateChangedEvent @event, CancellationToken cancellationToken = default) =>
    hub.Clients.All.Receive(new RealtimeMessage(RealtimeMessageTypes.ControlStateChanged, ControlStateChangeDto.FromEntity(@event.Change)));

  public Task HandleAsync(EnvironmentReadingChangedEvent @event, CancellationToken cancellationToken = default) =>
    hub.Clients.All.Receive(new RealtimeMessage(RealtimeMessageTypes.EnvironmentReadingChanged, @event.Reading));

  public Task HandleAsync(ThermostatConfigChangedEvent @event, CancellationToken cancellationToken = default) =>
    hub.Clients.All.Receive(
      new RealtimeMessage(RealtimeMessageTypes.ThermostatSettingsChanged, ThermostatSettingsPayload.FromEntity(@event.Thermostat))
    );

  public Task HandleAsync(SensorChangedEvent @event, CancellationToken cancellationToken = default) =>
    hub.Clients.All.Receive(new RealtimeMessage(RealtimeMessageTypes.SensorChanged, SensorResponse.FromSensor(@event.Sensor)));

  public Task HandleAsync(SensorDeletedEvent @event, CancellationToken cancellationToken = default) =>
    hub.Clients.All.Receive(new RealtimeMessage(RealtimeMessageTypes.SensorDeleted, @event.SensorId));

  public Task HandleAsync(DeviceChangedEvent @event, CancellationToken cancellationToken = default) =>
    hub.Clients.All.Receive(new RealtimeMessage(RealtimeMessageTypes.DeviceChanged, DeviceChangedPayload.FromEntity(@event.Device)));

  public Task HandleAsync(DeviceDeletedEvent @event, CancellationToken cancellationToken = default) =>
    hub.Clients.All.Receive(new RealtimeMessage(RealtimeMessageTypes.DeviceDeleted, @event.Device.Id));

  public Task HandleAsync(DeviceStateChangedEvent @event, CancellationToken cancellationToken = default) =>
    hub.Clients.All.Receive(new RealtimeMessage(RealtimeMessageTypes.DeviceStateChanged, @event.States));

  public Task HandleAsync(RelayStatesChangedEvent @event, CancellationToken cancellationToken = default) =>
    hub.Clients.All.Receive(new RealtimeMessage(RealtimeMessageTypes.RelayStatesChanged, @event.States));

  public Task HandleAsync(WeatherForecastChangedEvent @event, CancellationToken cancellationToken = default) =>
    hub.Clients.All.Receive(new RealtimeMessage(RealtimeMessageTypes.WeatherForecastChanged, @event.Forecast));

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
    services.AddScoped<IEventHandler<SensorChangedEvent>, SignalRBroadcastService>();
    services.AddScoped<IEventHandler<SensorDeletedEvent>, SignalRBroadcastService>();
    services.AddScoped<IEventHandler<DeviceChangedEvent>, SignalRBroadcastService>();
    services.AddScoped<IEventHandler<DeviceDeletedEvent>, SignalRBroadcastService>();
    services.AddScoped<IEventHandler<DeviceStateChangedEvent>, SignalRBroadcastService>();
    services.AddScoped<IEventHandler<RelayStatesChangedEvent>, SignalRBroadcastService>();
    services.AddScoped<IEventHandler<WeatherForecastChangedEvent>, SignalRBroadcastService>();
    services.AddScoped<IEventHandler<SendNotificationEvent>, SignalRBroadcastService>();
    services.AddScoped<IEventHandler<DismissNotificationEvent>, SignalRBroadcastService>();
    services.AddScoped<IEventHandler<PreferencesChangedEvent>, SignalRBroadcastService>();
  }
}
