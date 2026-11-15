using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Humidifiers;

public record GetHumidifiersRequest() : IRequest<GetHumidifiersResponse>;

public record GetHumidifiersResponse(List<HumidifierResponse> Humidifiers);

public class GetHumidifiersHandler(KelvinContext context) : IHandler<GetHumidifiersRequest, GetHumidifiersResponse>
{
  public async Task<Result<GetHumidifiersResponse>> HandleAsync(GetHumidifiersRequest request, CancellationToken ct = default)
  {
    var devices = await context
      .Devices.AsNoTracking()
      .OfType<HumidifierDevice>()
      .Where(d => d.DeletedAt == null)
      .OrderBy(d => d.Name)
      .ToListAsync(ct);
    return Result<GetHumidifiersResponse>.Success(new GetHumidifiersResponse(devices.Select(HumidifierResponse.FromDevice).ToList()));
  }
}

public class GetHumidifiersEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet(
        "/api/humidifiers",
        async (IHandler<GetHumidifiersRequest, GetHumidifiersResponse> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new GetHumidifiersRequest(), ct);
          return Results.Ok(result.Value);
        }
      )
      .WithName("GetHumidifiers")
      .WithTags("Humidifiers");
  }
}

public class GetHumidifiersRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<GetHumidifiersRequest, GetHumidifiersResponse>, GetHumidifiersHandler>();
  }
}
