using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Sensors;

public record GetSensorsRequest() : IRequest<GetSensorsResponse>;

public record GetSensorsResponse(List<SensorResponse> Sensors);

public class GetSensorsHandler(KelvinContext context) : IHandler<GetSensorsRequest, GetSensorsResponse>
{
  public async Task<Result<GetSensorsResponse>> HandleAsync(GetSensorsRequest request, CancellationToken ct = default)
  {
    var sensors = await context.Sensors.Select(s => SensorResponse.FromSensor(s)).ToListAsync(ct);
    return Result<GetSensorsResponse>.Success(new GetSensorsResponse(sensors));
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
