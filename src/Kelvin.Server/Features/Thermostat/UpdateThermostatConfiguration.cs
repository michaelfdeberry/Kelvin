using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kelvin.Server.Features.Thermostat;

public record UpdateThermostatConfigurationRequest(
  float HysteresisC,
  bool CO2RecirculationEnabled,
  CO2RecirculationMode CO2RecirculationMode,
  ushort CO2RecirculationTriggerPpm,
  ushort CO2RecirculationSatisfiedPpm,
  int CO2RecirculationDurationMs,
  bool HeatingAutoBalanceEnabled,
  float HeatingBalanceTriggerC,
  float HeatingBalanceSatisfiedC,
  int HeatingAutoBalanceDurationMs
) : IRequest;

public static class UpdateThermostatConfigurationErrors
{
  public static readonly Error ThermostatNotFound = new(
    "UpdateThermostatConfiguration.ThermostatNotFound",
    "The thermostat with the specified ID was not found."
  );

  public static readonly Error InvalidHysteresis = new(
    "UpdateThermostatConfiguration.InvalidHysteresis",
    "Hysteresis must be between 0.3 and 2.0 degrees Celsius."
  );

  public static readonly Error InvalidRecirculation = new(
    "UpdateThermostatConfiguration.InvalidRecirculation",
    "CO2 recirculation must stop below its trigger level and run for a positive duration."
  );

  public static readonly Error InvalidBalancing = new(
    "UpdateThermostatConfiguration.InvalidBalancing",
    "Heating balance must stop below its trigger and run for a positive duration."
  );
}

public class UpdateThermostatConfigurationHandler(KelvinContext context, IMemoryCache cache, IDispatcher dispatcher)
  : IHandler<UpdateThermostatConfigurationRequest>
{
  private const float MinHysteresisC = 0.3f;
  private const float MaxHysteresisC = 2.0f;

  public async Task<Result> HandleAsync(UpdateThermostatConfigurationRequest request, CancellationToken ct = default)
  {
    if (request.HysteresisC is < MinHysteresisC or > MaxHysteresisC)
    {
      return Result.Failure(UpdateThermostatConfigurationErrors.InvalidHysteresis);
    }

    if (request.CO2RecirculationSatisfiedPpm >= request.CO2RecirculationTriggerPpm || request.CO2RecirculationDurationMs <= 0)
    {
      return Result.Failure(UpdateThermostatConfigurationErrors.InvalidRecirculation);
    }

    if (
      request.HeatingBalanceSatisfiedC < 0
      || request.HeatingBalanceSatisfiedC >= request.HeatingBalanceTriggerC
      || request.HeatingAutoBalanceDurationMs <= 0
    )
    {
      return Result.Failure(UpdateThermostatConfigurationErrors.InvalidBalancing);
    }

    var thermostat = await context.Thermostats.Include(t => t.SetPoints).Include(t => t.Schedules).FirstOrDefaultAsync(ct);
    if (thermostat is null)
    {
      return Result.Failure(UpdateThermostatConfigurationErrors.ThermostatNotFound);
    }

    // the hysteresis widens the safety band, so the existing targets must still be safe with the new value
    var safetyResult = await dispatcher.DispatchAsync(
      new ValidateThermostatSafetyRequest(
        new ThermostatProjection(
          request.HysteresisC,
          thermostat.SetPoints.Where(s => s.DeletedAt is null).Select(s => new SetPointProjection(s.Id, s.Type, s.TargetTemperatureC)),
          thermostat
            .Schedules.Where(s => s.DeletedAt is null)
            .Select(s => new ScheduleProjection(s.Id, s.Type, s.StartTime, s.EndTime, s.TargetTemperatureC)),
          thermostat.HeatingLockoutC,
          thermostat.CoolingLockoutC
        )
      ),
      ct
    );

    if (safetyResult.IsFailure)
    {
      return safetyResult;
    }

    thermostat.HysteresisC = request.HysteresisC;
    thermostat.CO2RecirculationEnabled = request.CO2RecirculationEnabled;
    thermostat.CO2RecirculationMode = request.CO2RecirculationMode;
    thermostat.CO2RecirculationTriggerPpm = request.CO2RecirculationTriggerPpm;
    thermostat.CO2RecirculationSatisfiedPpm = request.CO2RecirculationSatisfiedPpm;
    thermostat.CO2RecirculationDurationMs = request.CO2RecirculationDurationMs;
    thermostat.HeatingAutoBalanceEnabled = request.HeatingAutoBalanceEnabled;
    thermostat.HeatingBalanceTriggerC = request.HeatingBalanceTriggerC;
    thermostat.HeatingBalanceSatisfiedC = request.HeatingBalanceSatisfiedC;
    thermostat.HeatingAutoBalanceDurationMs = request.HeatingAutoBalanceDurationMs;

    await context.SaveChangesAsync(ct);
    cache.Remove(ThermostatCache.Key);

    await dispatcher.PublishAsync(new ThermostatConfigChangedEvent(), ct);
    return Result.Success();
  }
}

public class UpdateThermostatConfigurationEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPut(
        "/api/thermostat/configuration",
        async (UpdateThermostatConfigurationRequest request, IHandler<UpdateThermostatConfigurationRequest> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(request, ct);
          if (result.IsFailure)
          {
            if (result.Error == UpdateThermostatConfigurationErrors.ThermostatNotFound)
            {
              return Results.NotFound(result.Error);
            }

            return Results.BadRequest(result.Error);
          }

          return Results.NoContent();
        }
      )
      .WithName("UpdateThermostatConfiguration")
      .WithTags("Thermostats");
  }
}

public class UpdateThermostatConfigurationRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<UpdateThermostatConfigurationRequest>, UpdateThermostatConfigurationHandler>();
  }
}
