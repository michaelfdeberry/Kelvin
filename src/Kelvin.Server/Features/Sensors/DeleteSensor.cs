using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kelvin.Server.Features.Sensors;

public record DeleteSensorRequest(Guid SensorId) : IRequest;

public static class DeleteSensorErrors
{
  public static readonly Error DefaultError = new("DeleteSensor.Failed", "An error occurred processing the request.");
}

public class DeleteSensorHandler(KelvinContext context, IMemoryCache cache) : IHandler<DeleteSensorRequest>
{
  public async Task<Result> HandleAsync(DeleteSensorRequest request, CancellationToken ct = default)
  {
    await context.Sensors.Where(s => s.Id == request.SensorId).ExecuteUpdateAsync(s => s.SetProperty(s => s.DeletedAt, DateTime.UtcNow), ct);
    cache.Remove(SensorsCache.Key);

    return Result.Success();
  }
}

public class DeleteSensorEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapDelete(
        "/api/sensors/{id}",
        async (Guid id, IHandler<DeleteSensorRequest> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new(id), ct);

          if (result.IsFailure)
          {
            return Results.InternalServerError(result.Error);
          }

          return Results.NoContent();
        }
      )
      .WithName("DeleteSensor")
      .WithTags("FeatureGroup");
  }
}

public class DeleteSensorRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<DeleteSensorRequest>, DeleteSensorHandler>();
  }
}
