using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Messaging;

namespace Kelvin.Server.Features.Notifications;

public record DismissNotificationRequest(Guid NotificationId) : IRequest;

public static class DismissNotificationErrors
{
  public static readonly Error NotFoundError = new("DismissNotification.NotFound", "Unable to find the specified notification.");
}

public class DismissNotificationHandler(KelvinContext context, IDispatcher dispatcher) : IHandler<DismissNotificationRequest>
{
  public async Task<Result> HandleAsync(DismissNotificationRequest request, CancellationToken ct = default)
  {
    var notification = await context.Notifications.FindAsync([request.NotificationId], ct);
    if (notification is null)
    {
      return Result.Failure(DismissNotificationErrors.NotFoundError);
    }

    context.Notifications.Remove(notification);
    await context.SaveChangesAsync(ct);

    await dispatcher.PublishAsync(new DismissNotificationEvent(notification.Id), ct);

    return Result.Success();
  }
}

public class DismissNotificationEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapDelete(
        "/api/notifications/{notificationId}",
        async (Guid notificationId, IHandler<DismissNotificationRequest> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new DismissNotificationRequest(notificationId), ct);

          if (result.IsFailure)
          {
            return Results.InternalServerError(result.Error);
          }

          return Results.Ok();
        }
      )
      .WithName("DismissNotification")
      .WithTags("Notifications");
  }
}

public class DismissNotificationRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<DismissNotificationRequest>, DismissNotificationHandler>();
  }
}
