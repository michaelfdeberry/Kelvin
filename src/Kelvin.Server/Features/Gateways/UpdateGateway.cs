using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kelvin.Server.Features.Gateways;

public record UpdateGatewayRequest(
  int? HeatingPin,
  int? FanPin,
  int? CoolingPin,
  int? ControlPin,
  int? MinimumOffDurationMinutes,
  int? MinimumOnDurationMinutes
) : IRequest;

public static class UpdateGatewayErrors
{
  public static readonly Error NotFound = new("UpdateGateway.NotFound", "The gateway hasn't been registered yet. Ensure the gateway is connected.");
}

public class UpdateGatewayHandler(KelvinContext context, IMemoryCache cache) : IHandler<UpdateGatewayRequest>
{
  public async Task<Result> HandleAsync(UpdateGatewayRequest request, CancellationToken ct = default)
  {
    var gateway = await context.Gateways.FirstOrDefaultAsync(ct);
    if (gateway is null)
      return Result.Failure(UpdateGatewayErrors.NotFound);

    gateway.HeatingPin = request.HeatingPin;
    gateway.FanPin = request.FanPin;
    gateway.CoolingPin = request.CoolingPin;
    gateway.ControlPin = request.ControlPin;
    gateway.MinimumOffDurationMinutes = request.MinimumOffDurationMinutes;
    gateway.MinimumOnDurationMinutes = request.MinimumOnDurationMinutes;

    await context.SaveChangesAsync(ct);
    cache.Remove(GatewayCache.Key);

    return Result.Success();
  }
}

public class UpdateGatewayEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPut(
        "/api/gateway",
        async (UpdateGatewayRequest request, IHandler<UpdateGatewayRequest> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(request, ct);
          if (result.IsFailure)
          {
            if (result.Error == UpdateGatewayErrors.NotFound)
              return Results.Json(result.Error, statusCode: StatusCodes.Status412PreconditionFailed);

            return Results.BadRequest(result.Error);
          }

          return Results.NoContent();
        }
      )
      .WithName("UpdateGateway")
      .WithTags("Gateway");
  }
}

public class UpdateGatewayRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<UpdateGatewayRequest>, UpdateGatewayHandler>();
  }
}
