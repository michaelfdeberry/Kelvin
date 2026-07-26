using System.Net.NetworkInformation;
using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kelvin.Server.Features.Gateways;

public record SaveGatewayMacAddressRequest(string MacAddress) : IRequest;

public static class SaveGatewayMacAddressErrors
{
  public static readonly Error InvalidMacAddress = new("SaveGatewayMacAddress.InvalidMacAddress", "The specified MAC address is invalid.");
}

public class SaveGatewayMacAddressHandler(KelvinContext context, IMemoryCache cache) : IHandler<SaveGatewayMacAddressRequest>
{
  public async Task<Result> HandleAsync(SaveGatewayMacAddressRequest request, CancellationToken ct = default)
  {
    if (!PhysicalAddress.TryParse(request.MacAddress, out _))
      return Result.Failure(SaveGatewayMacAddressErrors.InvalidMacAddress);

    var gateway = await context.Gateways.FirstOrDefaultAsync(ct);
    if (gateway is null)
    {
      context.Gateways.Add(new() { MacAddress = request.MacAddress });
    }
    else
    {
      gateway.MacAddress = request.MacAddress;
    }

    await context.SaveChangesAsync(ct);
    cache.Remove(GatewayCache.Key);
    return Result.Success();
  }
}

public class SaveGatewayMacAddressFeature : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<SaveGatewayMacAddressRequest>, SaveGatewayMacAddressHandler>();
  }
}
