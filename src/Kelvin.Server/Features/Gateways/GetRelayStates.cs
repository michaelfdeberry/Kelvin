using Kelvin.Server.Application;
using Kelvin.Server.Models;
using Kelvin.Server.Services;

namespace Kelvin.Server.Features.Gateways;

public record GetRelayStatesRequest() : IRequest<GetRelayStatesResponse>;

public record GetRelayStatesResponse(bool? Heating, bool? Cooling, bool? Fan, bool? Control)
{
  public static GetRelayStatesResponse FromState(RelayState state) => new(state.Heating, state.Cooling, state.Fan, state.Control);
}

public class GetRelayStatesHandler(IRelayController relayController) : IHandler<GetRelayStatesRequest, GetRelayStatesResponse>
{
  public async Task<Result<GetRelayStatesResponse>> HandleAsync(GetRelayStatesRequest request, CancellationToken ct = default)
  {
    var response = GetRelayStatesResponse.FromState(relayController.GetState());
    return Result<GetRelayStatesResponse>.Success(response);
  }
}

public class GetRelayStatesEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet(
        "/api/gateway/relays/states",
        async (IHandler<GetRelayStatesRequest, GetRelayStatesResponse> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new GetRelayStatesRequest(), ct);

          if (result.IsFailure)
          {
            return Results.InternalServerError(result.Error);
          }

          return Results.Ok(result.Value);
        }
      )
      .WithName("GetRelayStates")
      .WithTags("Gateway");
  }
}

public class GetRelayStatesRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<GetRelayStatesRequest, GetRelayStatesResponse>, GetRelayStatesHandler>();
  }
}
