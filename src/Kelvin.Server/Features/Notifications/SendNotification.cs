using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;

namespace Kelvin.Server.Features.Notifications;

public record SendNotificationRequest(
  string Message,
  NotificationType Type = NotificationType.Information,
  bool Dismissible = true,
  string? Heading = null,
  bool? Banner = false,
  int? Duration = null
) : IRequest;

public static class SendNotificationErrors
{
  public static readonly Error DefaultError = new("SendNotification.Failed", "An error occurred processing the request.");
}

public class SendNotificationHandler(KelvinContext context, IDispatcher dispatcher) : IHandler<SendNotificationRequest>
{
  public async Task<Result> HandleAsync(SendNotificationRequest request, CancellationToken ct = default)
  {
    var notification = new Notification()
    {
      Message = request.Message,
      Type = request.Type,
      Dismissible = request.Dismissible,
      Heading = request.Heading,
      Banner = request.Banner,
      Duration = request.Duration,
    };

    context.Notifications.Add(notification);
    await context.SaveChangesAsync(ct);
    await dispatcher.PublishAsync(new SendNotificationEvent(notification), ct);

    return Result.Success();
  }
}

public class SendNotificationRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<SendNotificationRequest>, SendNotificationHandler>();
  }
}
