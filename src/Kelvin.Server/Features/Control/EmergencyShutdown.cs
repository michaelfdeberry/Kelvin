using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Data;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Hubs;
using Kelvin.Server.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kelvin.Server.Features.Control;

public record EmergencyShutdownRequest(string Reason) : IRequest;

public class EmergencyShutdownHandler(
  KelvinContext context,
  ILogger<EmergencyShutdownHandler> logger,
  IMemoryCache cache,
  IControlChannel controlChannel,
  IHubContext<ControlHub, IControlClient> controlHub,
  IHubContext<NotificationsHub, INotificationsClient> notificationHub
) : IHandler<EmergencyShutdownRequest>
{
  public async Task<Result> HandleAsync(EmergencyShutdownRequest request, CancellationToken ct = default)
  {
    logger.LogCritical("Emergency shutdown requested: {Reason}", request.Reason);
    await controlChannel.WriteAsync(new ControlMessage(ControlState.Disable, Reason: request.Reason), ct);

    // update the thermostat outside of the update thermostat handler to
    // prevent sending multiple control messages for the same state change.
    var thermostat = await context.Thermostats.FirstAsync(ct);
    thermostat.Mode = RunMode.Disabled;
    thermostat.FanEnabled = false;
    await context.SaveChangesAsync(ct);
    cache.Remove(ThermostatCache.Key);

    var notification = new Notification(request.Reason, NotificationType.Error, Heading: "Emergency Shutdown", Banner: true);
    await notificationHub.Clients.All.Notify(notification);
    await controlHub.Clients.All.ThermostatStateChanged();

    return Result.Success();
  }
}

public class EmergencyShutdownRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<EmergencyShutdownRequest>, EmergencyShutdownHandler>();
  }
}
