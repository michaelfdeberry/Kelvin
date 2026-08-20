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
  public static readonly Error InvalidPin = new("UpdateGateway.InvalidPin", "The provided pin is invalid. Ensure the pin is a valid GPIO pin.");

  public static readonly Error MinimumOffDurationTooShort = new(
    "UpdateGateway.MinimumOffDurationTooShort",
    "The minimum off duration is too short. Ensure the minimum off duration is at least 3 minutes."
  );
  public static readonly Error MinimumOnDurationTooShort = new(
    "UpdateGateway.MinimumOnDurationTooShort",
    "The minimum on duration is too short. Ensure the minimum on duration is at least 2 minutes."
  );

  public static readonly Error OverlappingPins = new(
    "UpdateGateway.OverlappingPins",
    "The provided pins overlap. Ensure that each pin is unique and not used for multiple purposes."
  );
}

public class UpdateGatewayHandler(KelvinContext context, IMemoryCache cache) : IHandler<UpdateGatewayRequest>
{
  public async Task<Result> HandleAsync(UpdateGatewayRequest request, CancellationToken ct = default)
  {
    var gateway = await context.Gateways.FirstOrDefaultAsync(ct);
    if (gateway is null)
      return Result.Failure(UpdateGatewayErrors.NotFound);

    if (request.MinimumOffDurationMinutes is int minOff && minOff < 3)
      return Result.Failure(UpdateGatewayErrors.MinimumOffDurationTooShort);

    if (request.MinimumOnDurationMinutes is int minOn && minOn < 2)
      return Result.Failure(UpdateGatewayErrors.MinimumOnDurationTooShort);

    if (
      new[] { request.HeatingPin, request.FanPin, request.CoolingPin, request.ControlPin }
        .Where(pin => pin is not null)
        .GroupBy(pin => pin!.Value)
        .Any(group => group.Count() > 1)
    )
      return Result.Failure(UpdateGatewayErrors.OverlappingPins);

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
