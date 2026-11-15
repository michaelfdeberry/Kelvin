using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Thermostat;

public record UpdateFanStateRequest(bool FanEnabled) : IRequest;

public static class UpdateFanStateErrors
{
  public static readonly Error DefaultError = new("UpdateFanState.Failed", "An error occurred processing the request.");
}

public class UpdateFanStateHandler(KelvinContext context, IControlChannel controlChannel) : IHandler<UpdateFanStateRequest>
{
  public async Task<Result> HandleAsync(UpdateFanStateRequest request, CancellationToken ct = default)
  {
    var thermostat = await context
      .Thermostats.Include(t => t.SetPoints)
      .Include(t => t.Schedules)
      .OrderByDescending(t => t.CreatedAt)
      .FirstOrDefaultAsync(ct);

    if (thermostat is null)
      return Result.Failure(UpdateThermostatErrors.ThermostatNotFound);

    if (thermostat.Mode is RunMode.Disabled or RunMode.Off)
      return Result.Success();

    if (thermostat.FanEnabled == request.FanEnabled)
      return Result.Success();

    thermostat.FanEnabled = request.FanEnabled;
    await context.SaveChangesAsync(ct);

    var controlContext = new ControlMessage(
      State: thermostat.FanEnabled ? ControlState.FanOn : ControlState.FanOff,
      Mode: thermostat.Mode,
      HysteresisC: thermostat.HysteresisC,
      Reason: "Fan state changed"
    );
    await controlChannel.WriteAsync(controlContext, ct);

    return Result.Success();
  }
}

public class UpdateFanStateRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<UpdateFanStateRequest>, UpdateFanStateHandler>();
  }
}
