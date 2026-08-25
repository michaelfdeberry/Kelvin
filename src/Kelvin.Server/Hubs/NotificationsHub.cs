using Kelvin.Server.Application;
using Kelvin.Server.Models;
using Microsoft.AspNetCore.SignalR;

namespace Kelvin.Server.Hubs;

public interface INotificationsClient
{
  Task Notify(Notification notification);
}

public class NotificationsHub : Hub<INotificationsClient> { }

public class NotificationHubEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapHub<NotificationsHub>("/hubs/notifications");
  }
}
