using Microsoft.Extensions.Hosting;

namespace Kelvin.Server.Services;

public class ControlService : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      // TODO: Implement control logic here
      await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
    }
  }
}
