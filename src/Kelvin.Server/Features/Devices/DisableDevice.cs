using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Devices;

public record DisableDeviceRequest(Guid DeviceId) : IRequest;

public static class DisableDeviceErrors
{
  public static readonly Error NotFoundError = new("DisableDevice.NotFound", "The requested device was not found.");
}

public class DisableDeviceHandler(KelvinContext context, IDispatcher dispatcher) : IHandler<DisableDeviceRequest>
{
  public async Task<Result> HandleAsync(DisableDeviceRequest request, CancellationToken ct = default)
  {
    var device = await context.Devices.FirstOrDefaultAsync(d => d.Id == request.DeviceId && d.DeletedAt == null, ct);
    if (device is null)
    {
      return Result.Failure(DisableDeviceErrors.NotFoundError);
    }

    device.Enabled = false;
    await context.SaveChangesAsync(ct);

    await dispatcher.PublishAsync(new DevicesChangedEvent(), ct);
    return Result.Success();
  }
}

public class DisableDeviceEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapDelete(
        "/api/devices/{deviceId:guid}/disable",
        async (Guid deviceId, IHandler<DisableDeviceRequest> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new(deviceId), ct);
          if (result.IsFailure)
          {
            if (result.Error == DisableDeviceErrors.NotFoundError)
            {
              return Results.NotFound(result.Error);
            }

            return Results.InternalServerError(result.Error);
          }

          return Results.NoContent();
        }
      )
      .WithName("DisableDevice")
      .WithTags("Devices");
  }
}

public class DisableDeviceRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<DisableDeviceRequest>, DisableDeviceHandler>();
  }
}
