using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Hmi;

public record GetHmiMacAddressesRequest() : IRequest<GetHmiMacAddressesResponse>;

public record GetHmiMacAddressesResponse(IReadOnlyList<string> MacAddresses);

public class GetHmiMacAddressesHandler(KelvinContext context) : IHandler<GetHmiMacAddressesRequest, GetHmiMacAddressesResponse>
{
  public async Task<Result<GetHmiMacAddressesResponse>> HandleAsync(GetHmiMacAddressesRequest request, CancellationToken ct = default)
  {
    var macAddresses = await context
      .Hmis.Where(hmi => hmi.Sensor != null && hmi.Sensor.Enabled && hmi.Sensor.DeletedAt == null && hmi.MacAddress != null && hmi.DeletedAt == null)
      .Select(hmi => hmi.MacAddress!)
      .ToListAsync(ct);

    return Result<GetHmiMacAddressesResponse>.Success(new GetHmiMacAddressesResponse(macAddresses));
  }
}

public class GetHmiMacAddressesRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<GetHmiMacAddressesRequest, GetHmiMacAddressesResponse>, GetHmiMacAddressesHandler>();
  }
}
