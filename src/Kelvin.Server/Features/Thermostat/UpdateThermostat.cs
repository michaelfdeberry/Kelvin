using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kelvin.Server.Features.Thermostat;

public record UpdateThermostatRequest(RunMode Mode, bool FanEnabled) : IRequest;

public static class UpdateThermostatErrors
{
  public static readonly Error ThermostatNotFound = new("UpdateThermostat.ThermostatNotFound", "The thermostat with the specified ID was not found.");
}

public class UpdateThermostatHandler(KelvinContext context, IMemoryCache cache, IControlChannel controlChannel) : IHandler<UpdateThermostatRequest>
{
  public async Task<Result> HandleAsync(UpdateThermostatRequest request, CancellationToken ct = default)
  {
    var thermostat = await context.Thermostats.FirstOrDefaultAsync(ct);
    if (thermostat is null)
      return Result.Failure(UpdateThermostatErrors.ThermostatNotFound);

    thermostat.Mode = request.Mode;
    thermostat.FanEnabled = request.FanEnabled;

    if (thermostat.Mode == RunMode.Disabled)
    {
      await controlChannel.WriteAsync(new ControlMessage(ControlState.Disable), ct);
    }
    else
    {
      // any mode other than Disabled means Kelvin holds control, which is what energizes the control relay
      await controlChannel.WriteAsync(new ControlMessage(ControlState.Enable), ct);

      if (thermostat.Mode == RunMode.Off)
        await controlChannel.WriteAsync(new ControlMessage(ControlState.Idle), ct);
    }

    await context.SaveChangesAsync(ct);
    cache.Remove(ThermostatCache.Key);

    return Result.Success();
  }
}

public class UpdateThermostatEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPut(
        "/api/thermostat",
        async (UpdateThermostatRequest request, IHandler<UpdateThermostatRequest> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(request, ct);
          if (result.IsFailure)
          {
            if (result.Error == UpdateThermostatErrors.ThermostatNotFound)
              return Results.NotFound(result.Error);

            return Results.BadRequest(result.Error);
          }

          return Results.NoContent();
        }
      )
      .WithName("UpdateThermostat")
      .WithTags("Thermostats");
  }
}

public class UpdateThermostatFeatureRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<UpdateThermostatRequest>, UpdateThermostatHandler>();
  }
}
