using Kelvin.Server.Application;
using Kelvin.Server.Data;

namespace Kelvin.Server.Features.Sensors;

public record DisableSensorRequest(Guid SensorId) : IRequest;

public static class DisableSensorErrors
{
  public static readonly Error NotFoundError = new("DisableSensor.NotFound", "The requested sensor was not found.");
}

public class DisableSensorHandler(KelvinContext context) : IHandler<DisableSensorRequest>
{
  public async Task<Result> HandleAsync(DisableSensorRequest request, CancellationToken ct = default)
  {
    var sensor = await context.Sensors.FindAsync([request.SensorId], ct);
    if (sensor is null)
    {
      return Result.Failure(DisableSensorErrors.NotFoundError);
    }

    sensor.Enabled = false;
    await context.SaveChangesAsync(ct);

    return Result.Success();
  }
}

public class DisableSensorEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapDelete(
        "/api/sensors/{sensorId}/disable",
        async (Guid sensorId, IHandler<DisableSensorRequest> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new(sensorId), ct);
          if (result.IsFailure)
          {
            if (result.Error == DisableSensorErrors.NotFoundError)
            {
              return Results.NotFound(result.Error);
            }

            return Results.InternalServerError(result.Error);
          }

          return Results.NoContent();
        }
      )
      .WithName("DisableSensor")
      .WithTags("Sensors");
  }
}

public class DisableSensorRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<DisableSensorRequest>, DisableSensorHandler>();
  }
}
