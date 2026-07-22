using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Gateways;

public interface IGatewayManager
{
  Task UpdateGatewayMacAddress(string macAddress, CancellationToken stoppingToken);
}

public class GatewayManager(KelvinContext context) : IGatewayManager
{
  public async Task UpdateGatewayMacAddress(string macAddress, CancellationToken stoppingToken)
  {
    // currently there is only going to be support for one gateway.
    var gateway = await context.Gateways.FirstOrDefaultAsync(stoppingToken);
    if (gateway is null)
    {
      gateway = new Gateway();
      context.Gateways.Add(gateway);
    }
    gateway.MacAddress = macAddress;

    await context.SaveChangesAsync(stoppingToken);
  }
}
