namespace Kelvin.Server.Services;

using System.Threading;
using System.Threading.Tasks;
using Kelvin.Server.Application;
using Kelvin.Server.Features.Sensors;
using Microsoft.Extensions.Hosting;

public class TruncationService(ILogger<TruncationService> logger, IDispatcher dispatcher) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await dispatcher.DispatchAsync(new CleanupSensorPacketsRequest(), stoppingToken);
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Error while cleaning up sensor packets.");
      }

      // Any other cleanup tasks can be added here in the future
      // Runs the cleanup every 4 hours
      await Task.Delay(TimeSpan.FromHours(4), stoppingToken);
    }
  }
}
