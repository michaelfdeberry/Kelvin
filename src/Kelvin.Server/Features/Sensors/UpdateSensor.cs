using Kelvin.Server.Application;
using Kelvin.Server.Data;

namespace Kelvin.Server.Features.Sensors;

public record UpdateSensorRequest(Guid Id, SensorRequest Update) : IRequest;

public static class UpdateSensorErrors
{
  public static readonly Error DefaultError = new("UpdateSensor.NotFound", "The requested sensor was not found.");
}

public class UpdateSensorHandler(KelvinContext context) : IHandler<UpdateSensorRequest>
{
  public async Task<Result> HandleAsync(UpdateSensorRequest request, CancellationToken ct = default)
  {
    var sensor = await context.Sensors.FindAsync([request.Id], ct);
    if (sensor is null)
    {
      return Result.Failure(UpdateSensorErrors.DefaultError);
    }

    sensor.Name = request.Update.Name;
    sensor.MacAddress = request.Update.MacAddress;
    sensor.HasBattery = request.Update.HasBattery;
    sensor.HasHumiditySensor = request.Update.HasHumiditySensor;
    sensor.HasCO2Sensor = request.Update.HasCO2Sensor;
    await context.SaveChangesAsync(ct);

    return Result.Success();
  }
}

public class UpdateSensorEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPut(
        "/api/sensors/{id:guid}",
        async (Guid id, SensorRequest update, IHandler<UpdateSensorRequest> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new UpdateSensorRequest(id, update), ct);
          if (result.IsFailure)
          {
            return Results.BadRequest(result.Error);
          }

          return Results.NoContent();
        }
      )
      .WithName("UpdateSensor")
      .WithTags("Sensors");
  }
}

public class UpdateSensorFeatureRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<UpdateSensorRequest>, UpdateSensorHandler>();
  }
}
