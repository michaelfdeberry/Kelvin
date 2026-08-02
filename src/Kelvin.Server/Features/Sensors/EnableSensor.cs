using Kelvin.Server.Application;
using Kelvin.Server.Data;

namespace Kelvin.Server.Features.Sensors;

public record EnableSensorRequest(Guid SensorId) : IRequest;

public static class EnableSensorErrors
{
  public static readonly Error NotFoundError = new("EnableSensor.NotFound", "The requested sensor was not found.");
}

public class EnableSensorHandler(KelvinContext context) : IHandler<EnableSensorRequest>
{
  public async Task<Result> HandleAsync(EnableSensorRequest request, CancellationToken ct = default)
  {
    var sensor = await context.Sensors.FindAsync([request.SensorId], ct);
    if (sensor is null)
    {
      return Result.Failure(EnableSensorErrors.NotFoundError);
    }

    sensor.Enabled = true;
    await context.SaveChangesAsync(ct);

    return Result.Success();
  }
}

public class EnableSensorEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost(
        "/api/sensors/{sensorId}/enable",
        async (Guid sensorId, IHandler<EnableSensorRequest> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new(sensorId), ct);

          if (result.IsFailure)
          {
            if (result.Error == EnableSensorErrors.NotFoundError)
            {
              return Results.NotFound(result.Error);
            }

            return Results.InternalServerError(result.Error);
          }

          return Results.NoContent();
        }
      )
      .WithName("EnableSensor")
      .WithTags("Sensors");
  }
}

public class EnableSensorFeatureRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<EnableSensorRequest>, EnableSensorHandler>();
  }
}
