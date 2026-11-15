using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Devices;

public record GetDevicesRequest() : IRequest<GetDevicesResponse>;

public record GetDevicesResponse(List<DeviceResponse> Devices);

public class GetDevicesHandler(KelvinContext context) : IHandler<GetDevicesRequest, GetDevicesResponse>
{
  public async Task<Result<GetDevicesResponse>> HandleAsync(GetDevicesRequest request, CancellationToken ct = default)
  {
    var devices = await context.Devices.AsNoTracking().Where(d => d.DeletedAt == null).OrderBy(d => d.Name).ToListAsync(ct);
    return Result<GetDevicesResponse>.Success(new GetDevicesResponse(devices.Select(DeviceResponse.FromDevice).ToList()));
  }
}

public class GetDevicesEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet(
        "/api/devices",
        async (IHandler<GetDevicesRequest, GetDevicesResponse> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new GetDevicesRequest(), ct);
          return Results.Ok(result.Value);
        }
      )
      .WithName("GetDevices")
      .WithTags("Devices");
  }
}

public class GetDevicesRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<GetDevicesRequest, GetDevicesResponse>, GetDevicesHandler>();
  }
}
