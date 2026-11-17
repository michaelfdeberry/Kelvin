using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Thermostat;

public record GetSetPointsRequest() : IRequest<GetSetPointsResponse>;

public record SetPointResponse(Guid Id, RunType Type, float TargetTemperatureC)
{
  public static SetPointResponse FromEntity(SetPoint setPoint) => new(setPoint.Id, setPoint.Type, setPoint.TargetTemperatureC);
}

public record GetSetPointsResponse(IEnumerable<SetPointResponse> SetPoints);

public class GetSetPointsHandler(KelvinContext context) : IHandler<GetSetPointsRequest, GetSetPointsResponse>
{
  public async Task<Result<GetSetPointsResponse>> HandleAsync(GetSetPointsRequest request, CancellationToken ct = default)
  {
    var setPoints = await context.SetPoints.Where(sp => sp.DeletedAt == null).ToListAsync(ct);
    return Result<GetSetPointsResponse>.Success(new GetSetPointsResponse(setPoints.Select(SetPointResponse.FromEntity)));
  }
}

public class GetSetPointsEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet(
        "/api/thermostat/set-points",
        async (IHandler<GetSetPointsRequest, GetSetPointsResponse> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new GetSetPointsRequest(), ct);
          if (result.IsFailure)
          {
            return Results.InternalServerError(result.Error);
          }

          return Results.Ok(result.Value);
        }
      )
      .WithName("GetSetPoints")
      .WithTags("Thermostats");
  }
}

public class GetSetPointsRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<GetSetPointsRequest, GetSetPointsResponse>, GetSetPointsHandler>();
  }
}
