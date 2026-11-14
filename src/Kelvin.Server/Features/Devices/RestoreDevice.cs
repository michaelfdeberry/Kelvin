using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Devices;

public record RestoreDeviceRequest(Guid DeviceId) : IRequest;

public static class RestoreDeviceErrors
{
  public static readonly Error NotFoundError = new("RestoreDevice.NotFound", "The requested device was not found.");
}

public class RestoreDeviceHandler(KelvinContext context, IDispatcher dispatcher) : IHandler<RestoreDeviceRequest>
{
  public async Task<Result> HandleAsync(RestoreDeviceRequest request, CancellationToken ct = default)
  {
    var device = await context.Devices.FirstOrDefaultAsync(d => d.Id == request.DeviceId && d.DeletedAt != null, ct);
    if (device is null)
    {
      return Result.Failure(RestoreDeviceErrors.NotFoundError);
    }

    device.DeletedAt = null;
    await context.SaveChangesAsync(ct);

    await dispatcher.PublishAsync(new DevicesChangedEvent(), ct);
    return Result.Success();
  }
}

public class RestoreDeviceEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost(
        "/api/devices/{id:guid}",
        async (Guid id, IHandler<RestoreDeviceRequest> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new(id), ct);
          if (result.IsFailure)
          {
            if (result.Error == RestoreDeviceErrors.NotFoundError)
            {
              return Results.NotFound(result.Error);
            }

            return Results.InternalServerError(result.Error);
          }

          return Results.NoContent();
        }
      )
      .WithName("RestoreDevice")
      .WithTags("Devices");
  }
}

public class RestoreDeviceRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<RestoreDeviceRequest>, RestoreDeviceHandler>();
  }
}
