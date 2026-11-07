using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Devices;

public record UpdateDeviceRequest(Guid Id, DeviceRequest Update) : IRequest;

public static class UpdateDeviceErrors
{
  public static readonly Error NotFoundError = new("UpdateDevice.NotFound", "The requested device was not found.");
  public static readonly Error NameRequired = new("UpdateDevice.NameRequired", "The device name is required.");
}

public class UpdateDeviceHandler(KelvinContext context, IDispatcher dispatcher) : IHandler<UpdateDeviceRequest>
{
  public async Task<Result> HandleAsync(UpdateDeviceRequest request, CancellationToken ct = default)
  {
    var name = request.Update.Name?.Trim();
    if (string.IsNullOrEmpty(name))
    {
      return Result.Failure(UpdateDeviceErrors.NameRequired);
    }

    var device = await context.Devices.FirstOrDefaultAsync(d => d.Id == request.Id && d.DeletedAt == null, ct);
    if (device is null)
    {
      return Result.Failure(UpdateDeviceErrors.NotFoundError);
    }

    device.Name = name;
    await context.SaveChangesAsync(ct);

    await dispatcher.PublishAsync(new DevicesChangedEvent(), ct);
    return Result.Success();
  }
}

public class UpdateDeviceEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPut(
        "/api/devices/{id:guid}",
        async (Guid id, DeviceRequest update, IHandler<UpdateDeviceRequest> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new UpdateDeviceRequest(id, update), ct);
          if (result.IsFailure)
          {
            if (result.Error == UpdateDeviceErrors.NotFoundError)
            {
              return Results.NotFound(result.Error);
            }

            return Results.BadRequest(result.Error);
          }

          return Results.NoContent();
        }
      )
      .WithName("UpdateDevice")
      .WithTags("Devices");
  }
}

public class UpdateDeviceRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<UpdateDeviceRequest>, UpdateDeviceHandler>();
  }
}
