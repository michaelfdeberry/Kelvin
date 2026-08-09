using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Sensors;

public record CleanupSensorPacketsRequest() : IRequest;

public class CleanupSensorPacketsHandler(KelvinContext context) : IHandler<CleanupSensorPacketsRequest>
{
  private const int MAX_PACKET_AGE_DAYS = 30;

  public async Task<Result> HandleAsync(CleanupSensorPacketsRequest request, CancellationToken ct = default)
  {
    var cutoffDate = DateTime.UtcNow - TimeSpan.FromDays(MAX_PACKET_AGE_DAYS);
    await context.SensorPackets.Where(sp => sp.CreatedAt < cutoffDate).ExecuteDeleteAsync(ct);

    return Result.Success();
  }
}

public class CleanupSensorPacketsRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<CleanupSensorPacketsRequest>, CleanupSensorPacketsHandler>();
  }
}
