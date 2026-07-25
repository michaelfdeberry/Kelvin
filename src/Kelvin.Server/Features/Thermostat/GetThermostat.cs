using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kelvin.Server.Features.Thermostat;

public record GetThermostatRequest() : IRequest<GetThermostatResponse>;

public record GetThermostatResponse(Models.Thermostat Thermostat);

public record GetThermostatResponseDto(Guid Id, RunMode Mode, bool FanEnabled, float HysteresisC);

public static class ThermostatCache
{
  public const string Key = "thermostat";
}

public static class GetThermostatErrors
{
  public static readonly Error ThermostatNotFound = new("GetThermostat.ThermostatNotFound", "The thermostat with the specified ID was not found.");
  public static readonly Error DefaultError = new("GetThermostat.Failed", "An error occurred processing the request.");
}

public class GetThermostatHandler(KelvinContext context, IMemoryCache cache) : IHandler<GetThermostatRequest, GetThermostatResponse>
{
  public async Task<Result<GetThermostatResponse>> HandleAsync(GetThermostatRequest request, CancellationToken ct = default)
  {
    if (cache.TryGetValue(ThermostatCache.Key, out GetThermostatResponse? cachedResponse) && cachedResponse is not null)
      return Result<GetThermostatResponse>.Success(cachedResponse);

    // TODO: this will later be changed to get by id if multiple thermostats are ever supported, but for now we just return the first one
    var thermostat = await context.Thermostats.Include(t => t.SetPoints).Include(t => t.Schedules).FirstOrDefaultAsync(ct);
    if (thermostat is null)
    {
      return Result<GetThermostatResponse>.Failure(GetThermostatErrors.ThermostatNotFound);
    }

    var response = new GetThermostatResponse(thermostat);
    cache.Set(ThermostatCache.Key, response, TimeSpan.FromHours(24));

    return Result<GetThermostatResponse>.Success(response);
  }
}

public class GetThermostatEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet(
        "/api/thermostat",
        async (IHandler<GetThermostatRequest, GetThermostatResponse> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new GetThermostatRequest(), ct);
          if (result.IsFailure)
          {
            if (result.Error == GetThermostatErrors.ThermostatNotFound)
            {
              return Results.NotFound(result.Error);
            }

            return Results.InternalServerError(result.Error);
          }

          var thermostat = result.Value!.Thermostat!;
          return Results.Ok(new GetThermostatResponseDto(thermostat.Id, thermostat.Mode, thermostat.FanEnabled, thermostat.HysteresisC));
        }
      )
      .WithName("GetThermostat")
      .WithTags("Thermostats");
  }
}

public class GetThermostatFeatureRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<GetThermostatRequest, GetThermostatResponse>, GetThermostatHandler>();
  }
}
