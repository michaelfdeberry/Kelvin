using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Gateways;

public record GetGatewayRequest() : IRequest<GetGatewayResponse>;

public record GetGatewayResponse(Gateway Gateway);

public static class GetGatewayErrors
{
  public static readonly Error NotFound = new("GetGateway.NotFound", "The gateway hasn't been registered yet. Ensure the gateway is connected.");
}

public class GetGatewayHandler(KelvinContext context) : IHandler<GetGatewayRequest, GetGatewayResponse>
{
  public async Task<Result<GetGatewayResponse>> HandleAsync(GetGatewayRequest request, CancellationToken ct = default)
  {
    var gateway = await context.Gateways.FirstOrDefaultAsync(ct);
    if (gateway is null)
      return Result<GetGatewayResponse>.Failure(GetGatewayErrors.NotFound);

    return Result<GetGatewayResponse>.Success(new(gateway));
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
              return Results.NotFound(result.Error);

            return Results.BadRequest(result.Error);
          }

          // TODO: introduce mapping at some point to not send the domain model directly to the client
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
