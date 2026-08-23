using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kelvin.Server.Features.Sensors;

public record RestoreSensorRequest(Guid Id) : IRequest;

public static class RestoreSensorErrors
{
  public static readonly Error DefaultError = new("RestoreSensor.Failed", "An error occurred processing the request.");
}

public class RestoreSensorHandler(KelvinContext context, IMemoryCache cache) : IHandler<RestoreSensorRequest>
{
  public async Task<Result> HandleAsync(RestoreSensorRequest request, CancellationToken ct = default)
  {
    await context.Sensors.Where(s => s.Id == request.Id).ExecuteUpdateAsync(s => s.SetProperty(s => s.DeletedAt, (DateTime?)null), ct);
    cache.Remove(SensorsCache.Key);
    return Result.Success();
  }
}

public class RestoreSensorEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost(
        "/api/sensors/{id}",
        async (Guid id, IHandler<RestoreSensorRequest> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new(id), ct);
          if (result.IsFailure)
          {
            return Results.InternalServerError(result.Error);
          }

          return Results.NoContent();
        }
      )
      .WithName("RestoreSensor")
      .WithTags("Sensors");
  }
}

public class RestoreSensorRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<RestoreSensorRequest>, RestoreSensorHandler>();
  }
}
