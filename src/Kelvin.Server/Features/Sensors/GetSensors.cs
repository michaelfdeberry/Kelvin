using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kelvin.Server.Features.Sensors;

public record GetSensorsRequest() : IRequest<GetSensorsResponse>;

public record GetSensorsResponse(List<SensorResponse> Sensors);

public class GetSensorsHandler(KelvinContext context, IMemoryCache cache) : IHandler<GetSensorsRequest, GetSensorsResponse>
{
  public async Task<Result<GetSensorsResponse>> HandleAsync(GetSensorsRequest request, CancellationToken ct = default)
  {
    if (cache.TryGetValue(SensorsCache.Key, out GetSensorsResponse? cachedResponse) && cachedResponse is not null)
      return Result<GetSensorsResponse>.Success(cachedResponse);

    var sensors = await context.Sensors.Where(s => !s.DeletedAt.HasValue).Select(s => SensorResponse.FromSensor(s)).ToListAsync(ct);
    var response = new GetSensorsResponse(sensors);

    cache.Set(SensorsCache.Key, response, TimeSpan.FromHours(24));
    return Result<GetSensorsResponse>.Success(response);
  }
}

public class GetSensorsEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet(
        "/api/sensors",
        async (IHandler<GetSensorsRequest, GetSensorsResponse> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new GetSensorsRequest(), ct);
          return Results.Ok(result.Value);
        }
      )
      .WithName("GetSensors")
      .WithTags("Sensors");
  }
}

public class GetSensorsRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<GetSensorsRequest, GetSensorsResponse>, GetSensorsHandler>();
  }
}
