using System.Collections.Concurrent;
using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Sensors;

public record GetLatestReadingsRequest() : IRequest<GetLatestReadingsResponse>;

public record GetLatestReadingsResponse(EnvironmentReading Reading);

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
      .ToDictionaryAsync(p => p.SensorId!.Value, p => p, ct);

    var reading = new EnvironmentReading
    {
      Timestamp = DateTimeOffset.UtcNow,
      TemperatureC = latestPerSensor.Values.Average(p => p.TemperatureC),
      HumidityPercentage = latestPerSensor.Values.Average(p => p.HumidityPercentage),
      CO2LevelPpm = (float)latestPerSensor.Values.Average(p => p.CO2LevelPpm),
      Areas = new ConcurrentDictionary<Guid, SensorPacket>(latestPerSensor),
    };

    return Result<GetLatestReadingsResponse>.Success(new GetLatestReadingsResponse(reading));
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
          var result = await handler.HandleAsync(new GetLatestReadingsRequest(), ct);
          return Results.Ok(result.Value);
        }
      )
      .WithName("GetLatestReadings")
      .WithTags("Sensors");
  }
}

public class GetLatestReadingsRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<GetLatestReadingsRequest, GetLatestReadingsResponse>, GetLatestReadingsHandler>();
  }
}
