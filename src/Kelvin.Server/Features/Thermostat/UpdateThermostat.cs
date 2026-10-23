using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Data;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kelvin.Server.Features.Thermostat;

public record UpdateThermostatRequest(RunMode Mode, bool FanEnabled) : IRequest;

public static class UpdateThermostatErrors
{
  public static readonly Error ThermostatNotFound = new("UpdateThermostat.ThermostatNotFound", "The thermostat with the specified ID was not found.");
}

public class UpdateThermostatHandler(KelvinContext context, IMemoryCache cache, IControlChannel controlChannel, IDispatcher dispatcher)
  : IHandler<UpdateThermostatRequest>
{
  public async Task<Result> HandleAsync(UpdateThermostatRequest request, CancellationToken ct = default)
  {
    // For now there is only support for one thermostat, so we just get the most recently created one.
    // the order by is to get rid of the first or default without orderby warning.
    var thermostat = await context
      .Thermostats.Include(t => t.SetPoints)
      .Include(t => t.Schedules)
      .OrderByDescending(t => t.CreatedAt)
      .FirstOrDefaultAsync(ct);

    if (thermostat is null)
      return Result.Failure(UpdateThermostatErrors.ThermostatNotFound);

    if (thermostat.Mode != RunMode.Automatic && request.Mode == RunMode.Automatic)
    {
      // validate thermostat safety when switching to automatic mode. If the thermostat service runs
      // in automatic with invalid configuration it will trigger the failsafe and the user would need to intervene.
      // With this the modal can be popped open similar to how it behaves if configuration is missing before entering automatic mode.
      // This will maintain the current mode until the configuration is updated.
      var validationRequest = new ValidateThermostatSafetyRequest(
        new ThermostatProjection(
          thermostat.HysteresisC,
          thermostat.SetPoints.Where(s => s.DeletedAt is null).Select(s => new SetPointProjection(s.Id, s.Type, s.TargetTemperatureC)),
          thermostat
            .Schedules.Where(s => s.DeletedAt is null)
            .Select(s => new ScheduleProjection(s.Id, s.Type, s.StartTime, s.EndTime, s.TargetTemperatureC))
        )
      );
      var validationResult = await dispatcher.DispatchAsync(validationRequest, ct);
      if (validationResult.IsFailure)
      {
        return Result.Failure(validationResult.Error);
      }
    }

    var currentFanState = thermostat.FanEnabled;
    thermostat.Mode = request.Mode;
    thermostat.FanEnabled = request.FanEnabled;

    await context.SaveChangesAsync(ct);

    var controlContext = new ControlMessage(
      State: ControlState.Dwell,
      Mode: thermostat.Mode,
      HysteresisC: thermostat.HysteresisC,
      Reason: "the thermostat was updated"
    );

    if (thermostat.Mode == RunMode.Disabled)
    {
      await controlChannel.WriteAsync(controlContext with { State = ControlState.Disable }, ct);
    }
    else
    {
      // any mode other than Disabled means Kelvin holds control, which is what energizes the control relay
      await controlChannel.WriteAsync(controlContext with { State = ControlState.Enable }, ct);

      if (thermostat.Mode == RunMode.Off)
      {
        await controlChannel.WriteAsync(controlContext with { State = ControlState.Dwell }, ct);
      }

      if (thermostat.FanEnabled != currentFanState)
      {
        await controlChannel.WriteAsync(controlContext with { State = thermostat.FanEnabled ? ControlState.FanOn : ControlState.FanOff }, ct);
      }
    }

    cache.Remove(ThermostatCache.Key);

    await dispatcher.PublishAsync(new ThermostatConfigChangedEvent(), ct);
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

public class UpdateThermostatRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<UpdateThermostatRequest>, UpdateThermostatHandler>();
  }
}
