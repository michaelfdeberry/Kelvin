using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kelvin.Server.Features.Gateways;

public record GetGatewayRequest() : IRequest<GetGatewayResponse>;

public record GetGatewayResponse(
  string? MacAddress,
  int? HeatingPin,
  int? FanPin,
  int? CoolingPin,
  int? ControlPin,
  int? MinimumOffDurationMinutes,
  int? MinimumOnDurationMinutes
);

public static class GatewayCache
{
  public const string Key = "gateway";
}

public static class GetGatewayErrors
{
  public static readonly Error NotFound = new("GetGateway.NotFound", "The gateway hasn't been registered yet. Ensure the gateway is connected.");
}

public class GetGatewayHandler(KelvinContext context, IMemoryCache cache) : IHandler<GetGatewayRequest, GetGatewayResponse>
{
  public async Task<Result<GetGatewayResponse>> HandleAsync(GetGatewayRequest request, CancellationToken ct = default)
  {
    if (cache.TryGetValue(GatewayCache.Key, out GetGatewayResponse? cachedResponse) && cachedResponse is not null)
      return Result<GetGatewayResponse>.Success(cachedResponse);

    var gateway = await context.Gateways.FirstOrDefaultAsync(ct);
    if (gateway is null)
      return Result<GetGatewayResponse>.Failure(GetGatewayErrors.NotFound);

    var response = new GetGatewayResponse(
      gateway.MacAddress,
      gateway.HeatingPin,
      gateway.FanPin,
      gateway.CoolingPin,
      gateway.ControlPin,
      gateway.MinimumOffDurationMinutes,
      gateway.MinimumOnDurationMinutes
    );

    cache.Set(GatewayCache.Key, response, TimeSpan.FromHours(24));
    return Result<GetGatewayResponse>.Success(response);
  }
}

public class GetGatewayEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet(
        "/api/gateway",
        async (IHandler<GetGatewayRequest, GetGatewayResponse> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new GetGatewayRequest(), ct);
          if (result.IsFailure)
          {
            if (result.Error == GetGatewayErrors.NotFound)
              return Results.Json(result.Error, statusCode: StatusCodes.Status412PreconditionFailed);

            return Results.BadRequest(result.Error);
          }

          return Results.Ok(result.Value);
        }
      )
      .WithName("GetGateway")
      .WithTags("Gateway");
  }
}

public class GetGatewayFeatureRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<GetGatewayRequest, GetGatewayResponse>, GetGatewayHandler>();
  }
}
