using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Devices;

public record EnableDeviceRequest(Guid DeviceId) : IRequest;

public static class EnableDeviceErrors
{
  public static readonly Error NotFoundError = new("EnableDevice.NotFound", "The requested device was not found.");
}

public class EnableDeviceHandler(KelvinContext context, IDispatcher dispatcher) : IHandler<EnableDeviceRequest>
{
  public async Task<Result> HandleAsync(EnableDeviceRequest request, CancellationToken ct = default)
  {
    var device = await context.Devices.FirstOrDefaultAsync(d => d.Id == request.DeviceId && d.DeletedAt == null, ct);
    if (device is null)
    {
      return Result.Failure(EnableDeviceErrors.NotFoundError);
    }

    device.Enabled = true;
    await context.SaveChangesAsync(ct);

    await dispatcher.PublishAsync(new DevicesChangedEvent(), ct);
    return Result.Success();
  }
}

public class EnableDeviceEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost(
        "/api/devices/{deviceId:guid}/enable",
        async (Guid deviceId, IHandler<EnableDeviceRequest> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new(deviceId), ct);
          if (result.IsFailure)
          {
            if (result.Error == EnableDeviceErrors.NotFoundError)
            {
              return Results.NotFound(result.Error);
            }

            return Results.InternalServerError(result.Error);
          }

          return Results.NoContent();
        }
      )
      .WithName("EnableDevice")
      .WithTags("Devices");
  }
}

public class EnableDeviceRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<EnableDeviceRequest>, EnableDeviceHandler>();
  }
}
