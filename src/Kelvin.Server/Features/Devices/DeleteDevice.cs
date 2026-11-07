using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Devices;

public record DeleteDeviceRequest(Guid DeviceId) : IRequest;

public static class DeleteDeviceErrors
{
  public static readonly Error NotFoundError = new("DeleteDevice.NotFound", "The requested device was not found.");
}

public class DeleteDeviceHandler(KelvinContext context, IDispatcher dispatcher) : IHandler<DeleteDeviceRequest>
{
  public async Task<Result> HandleAsync(DeleteDeviceRequest request, CancellationToken ct = default)
  {
    var device = await context.Devices.FirstOrDefaultAsync(d => d.Id == request.DeviceId && d.DeletedAt == null, ct);
    if (device is null)
    {
      return Result.Failure(DeleteDeviceErrors.NotFoundError);
    }

    // Removing flips to a soft delete in the interceptor.
    context.Devices.Remove(device);
    await context.SaveChangesAsync(ct);

    await dispatcher.PublishAsync(new DevicesChangedEvent(), ct);
    return Result.Success();
  }
}

public class DeleteDeviceEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapDelete(
        "/api/devices/{id:guid}",
        async (Guid id, IHandler<DeleteDeviceRequest> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new(id), ct);
          if (result.IsFailure)
          {
            if (result.Error == DeleteDeviceErrors.NotFoundError)
            {
              return Results.NotFound(result.Error);
            }

            return Results.InternalServerError(result.Error);
          }

          return Results.NoContent();
        }
      )
      .WithName("DeleteDevice")
      .WithTags("Devices");
  }
}

public class DeleteDeviceRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<DeleteDeviceRequest>, DeleteDeviceHandler>();
  }
}
