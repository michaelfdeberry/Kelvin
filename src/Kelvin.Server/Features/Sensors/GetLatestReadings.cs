using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Sensors;

public record GetLatestReadingsRequest() : IRequest<GetLatestReadingsResponse>;

public record GetLatestReadingsResponse(IEnumerable<SensorPacket> Readings);

public static class GetLatestReadingsErrors
{
  public static readonly Error DefaultError = new("GetLatestReadings.Failed", "An error occurred processing the request.");
}

public class GetLatestReadingsHandler(KelvinContext context) : IHandler<GetLatestReadingsRequest, GetLatestReadingsResponse>
{
  public async Task<Result<GetLatestReadingsResponse>> HandleAsync(GetLatestReadingsRequest request, CancellationToken ct = default)
  {
    // Compute the latest timestamp per sensor in the database, then join back to fetch only those rows.
    var latestPerSensor = await context
      .SensorPackets.Where(p => p.SensorId != null)
      .GroupBy(p => p.SensorId)
      .Select(g => g.OrderByDescending(p => p.CreatedAt).First())
      .ToListAsync(ct);

    return Result<GetLatestReadingsResponse>.Success(new GetLatestReadingsResponse(latestPerSensor));
  }
}

public class GetLatestReadingsEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet(
        "/api/sensors/readings/latest",
        async (IHandler<GetLatestReadingsRequest, GetLatestReadingsResponse> handler, CancellationToken ct) =>
        {
          return Results.Ok(await handler.HandleAsync(new GetLatestReadingsRequest(), ct));
        }
      )
      .WithName("GetLatestReadings")
      .WithTags("Sensors");
  }
}

public class GetLatestReadingsFeatureRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<GetLatestReadingsRequest, GetLatestReadingsResponse>, GetLatestReadingsHandler>();
  }
}
