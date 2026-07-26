using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.GeoCoding;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Features.Weather;
using Kelvin.Server.Models;

namespace Kelvin.Server.Services;

public class ThermostatService(
  IControlChannel controlChannel,
  IEnvironmentChannel environmentChannel,
  IDispatcher dispatcher,
  TimeProvider time,
  ILogger<ThermostatService> logger
) : BackgroundService
{
  private static readonly Guid subscriberId = Guid.NewGuid();
  private ControlState _activeCall = ControlState.Dwell;
  private readonly float defaultHysteresis = 0.6f;
  private readonly float minSafeHysteresis = 0.3f;
  private readonly float maxSafeHysteresis = 2.0f;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        var environment = await environmentChannel.ReadAsync(subscriberId, stoppingToken);
        if (environment is null)
        {
          logger.LogWarning("Received null environment from channel.");
          continue;
        }

        var thermostatResult = await dispatcher.DispatchAsync<GetThermostatRequest, GetThermostatResponse>(new(), stoppingToken);
        thermostatResult.EnsureSuccess();

        var thermostat = thermostatResult.Value!.Thermostat;

        // What the loop knew when it made its decision, carried along so a recorded state change can explain itself.
        var context = new ControlContext(
          EnvironmentTemperatureC: environment.TemperatureC,
          HumidityPercentage: environment.HumidityPercentage,
          CO2LevelPpm: environment.CO2LevelPpm,
          HysteresisC: GetHysteresis(thermostat.HysteresisC),
          Mode: thermostat.Mode
        );

        if (thermostat.Mode == RunMode.Disabled)
        {
          logger.LogInformation("Thermostat is Disabled, skipping environment processing.");
          _activeCall = ControlState.Dwell;
          // the updating of the state will dispatch the control message, but do it here just in case.
          await controlChannel.WriteAsync(
            new ControlMessage(ControlState.Disable, context with { Reason = "the thermostat mode is Disabled" }),
            stoppingToken
          );
          continue;
        }

        // re-assert control every cycle so a restart re-energizes the control relay, noop if already energized.
        await controlChannel.WriteAsync(
          new ControlMessage(ControlState.Enable, context with { Reason = "the thermostat is enabled" }),
          stoppingToken
        );

        if (thermostat.Mode == RunMode.Off)
        {
          logger.LogInformation("Thermostat is Off, skipping environment processing.");
          _activeCall = ControlState.Dwell;
          await controlChannel.WriteAsync(
            new ControlMessage(ControlState.Dwell, context with { Reason = "the thermostat mode is Off" }),
            stoppingToken
          );
          continue;
        }

        var weatherResult = await dispatcher.DispatchAsync<GetWeatherForecastRequest, GetWeatherForecastResponse>(new(), stoppingToken);
        if (weatherResult.IsFailure && weatherResult.Error != GetCurrentLocationErrors.LocationNotConfigured)
        {
          // if the location is not configured, that is an acceptable reason to not have a forecast, so we will not log an error in that case.
          // otherwise, log the error but continue processing the environment, this will fall back to the non forecast based control logic.
          logger.LogError("Failed to get weather forecast: {Error}", weatherResult.Error);
        }

        var forecastTemperatureC = weatherResult.Value?.Current?.TemperatureC;
        // this returns the run mode for future use.
        // E.g. when humidifier support is added it would only run in heating mode
        // if a dehumidifier is added it might make sense to only run that when in cooling mode but the cooling mode is not active, etc.
        var runMode = await ProcessTemperature(environment, thermostat, forecastTemperatureC, context, stoppingToken);
        continue;
      }
      catch (OperationCanceledException)
      {
        logger.LogInformation("ThermostatService is stopping due to cancellation.");
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "An error occurred while processing the environment channel.");
      }
    }
  }

  private async Task<RunMode> ProcessTemperature(
    Models.Environment environment,
    Thermostat thermostat,
    float? forecastTemperatureC,
    ControlContext context,
    CancellationToken cancellationToken
  )
  {
    var environmentTemperatureC = environment.TemperatureC;
    var previousActiveCall = _activeCall;

    context = context with { ForecastTemperatureC = forecastTemperatureC };

    // Schedule windows are wall-clock TimeOnly values, so the local time is what they have to be compared against.
    var currentTimeOnly = TimeOnly.FromDateTime(time.GetLocalNow().DateTime);
    var activeSchedules = thermostat.Schedules.Where(s => s.Enabled && IsActive(currentTimeOnly, s.StartTime, s.EndTime));

    // overlapping schedules won't be allowed, so there will never be more than one heating or cooling schedule active at a time.
    var heatingSchedule = activeSchedules.FirstOrDefault(s => s.Type == RunType.Heating);
    var coolingSchedule = activeSchedules.FirstOrDefault(s => s.Type == RunType.Cooling);

    // similarly, there will only ever be at most one heating and one cooling set point configured
    var heatingSetPoint = thermostat.SetPoints.FirstOrDefault(sp => sp.Type == RunType.Heating);
    var coolingSetPoint = thermostat.SetPoints.FirstOrDefault(sp => sp.Type == RunType.Cooling);

    // gets the configured target temperature for heating and cooling, prioritizing schedules.
    var heatingTargetTemp = heatingSchedule?.TargetTemperatureC ?? heatingSetPoint?.TargetTemperatureC;
    var coolingTargetTemp = coolingSchedule?.TargetTemperatureC ?? coolingSetPoint?.TargetTemperatureC;

    // If there are no active heating or cooling target temps, log the information and do nothing.
    if (heatingTargetTemp is null && coolingTargetTemp is null)
    {
      logger.LogInformation("No active heating or cooling schedules or set points found.");
      _activeCall = ControlState.Dwell;
      await controlChannel.WriteAsync(
        new ControlMessage(ControlState.Dwell, context with { Reason = "no heating or cooling schedule or set point is active" }),
        cancellationToken
      );
      return RunMode.Off;
    }

    var heatingActivationTemp = heatingSchedule?.ActivationTemperatureC ?? heatingSetPoint?.ActivationTemperatureC;
    var coolingActivationTemp = coolingSchedule?.ActivationTemperatureC ?? coolingSetPoint?.ActivationTemperatureC;

    // if there is a forecast temp it means a current location is configured
    var useForecastForHeating = forecastTemperatureC is not null && heatingActivationTemp is not null;
    var useForecastForCooling = forecastTemperatureC is not null && coolingActivationTemp is not null;
    var forecastCallsForHeating = useForecastForHeating && forecastTemperatureC <= heatingActivationTemp;
    var forecastCallsForCooling = useForecastForCooling && forecastTemperatureC >= coolingActivationTemp;

    // determine if the environment temperature is below the heating target temp or above the cooling target temp
    var environmentBelowTarget = environmentTemperatureC < heatingTargetTemp;
    var environmentAboveTarget = environmentTemperatureC > coolingTargetTemp;

    bool callForHeating = false;
    bool callForCooling = false;
    var hysteresis = GetHysteresis(thermostat.HysteresisC);

    // determine if the environment temperature is below the heating target temp or above the cooling target temp, taking into account hysteresis
    if ((useForecastForHeating && forecastCallsForHeating) || !useForecastForHeating)
    {
      if (_activeCall == ControlState.Dwell && environmentTemperatureC <= (heatingTargetTemp - hysteresis))
      {
        callForHeating = true;
      }
      else if (_activeCall == ControlState.Heating && environmentTemperatureC < (heatingTargetTemp + hysteresis))
      {
        callForHeating = true;
      }
    }

    if ((useForecastForCooling && forecastCallsForCooling) || !useForecastForCooling)
    {
      if (_activeCall == ControlState.Dwell && environmentTemperatureC >= (coolingTargetTemp + hysteresis))
      {
        callForCooling = true;
      }
      else if (_activeCall == ControlState.Cooling && environmentTemperatureC > (coolingTargetTemp - hysteresis))
      {
        callForCooling = true;
      }
    }

    if ((callForHeating && previousActiveCall == ControlState.Cooling) || (callForCooling && previousActiveCall == ControlState.Heating))
    {
      // It should never be possible to switch directly between an active heating call and an active
      // cooling call; the hysteresis logic above is gated on _activeCall specifically to prevent this.
      // Treat it as a critical error and fall back to Dwell rather than flipping the call directly.
      logger.LogCritical(
        "Attempted to switch directly from {PreviousActiveCall} to a call for {RequestedCall} without an intermediate Dwell state.",
        previousActiveCall,
        callForHeating ? RunType.Heating : RunType.Cooling
      );
      _activeCall = ControlState.Dwell;
      await controlChannel.WriteAsync(
        new ControlMessage(ControlState.Dwell, context with { Reason = "an unsafe call transition was requested" }),
        cancellationToken
      );
      return RunMode.Off;
    }

    if (callForHeating && callForCooling)
    {
      // turn off the system, revert control to the dumb thermostat, and log the error.
      logger.LogCritical("Both heating and cooling conditions are met. This is an unexpected state.");
      _activeCall = ControlState.Dwell;
      await controlChannel.WriteAsync(
        new ControlMessage(ControlState.Disable, context with { Reason = "the heating and cooling conditions were both met" }),
        cancellationToken
      );
      return RunMode.Off;
    }

    if (callForHeating && (thermostat.Mode == RunMode.Heating || thermostat.Mode == RunMode.Automatic))
    {
      logger.LogInformation("Thermostat is in heating mode and conditions are met for heating.");
      _activeCall = ControlState.Heating;
      await controlChannel.WriteAsync(
        new ControlMessage(
          ControlState.Heating,
          context with
          {
            TargetTemperatureC = heatingTargetTemp,
            ScheduleId = heatingSchedule?.Id,
            SetPointId = heatingSchedule is null ? heatingSetPoint?.Id : null,
            Reason = "the heating conditions were met",
          }
        ),
        cancellationToken
      );
      return RunMode.Heating;
    }

    if (callForCooling && (thermostat.Mode == RunMode.Cooling || thermostat.Mode == RunMode.Automatic))
    {
      logger.LogInformation("Thermostat is in cooling mode and conditions are met for cooling.");
      _activeCall = ControlState.Cooling;
      await controlChannel.WriteAsync(
        new ControlMessage(
          ControlState.Cooling,
          context with
          {
            TargetTemperatureC = coolingTargetTemp,
            ScheduleId = coolingSchedule?.Id,
            SetPointId = coolingSchedule is null ? coolingSetPoint?.Id : null,
            Reason = "the cooling conditions were met",
          }
        ),
        cancellationToken
      );
      return RunMode.Cooling;
    }

    // no conditions are met for heating or cooling, so we will turn off the system
    logger.LogInformation("No conditions are met for heating or cooling, turning off the system.");
    _activeCall = ControlState.Dwell;
    await controlChannel.WriteAsync(
      new ControlMessage(ControlState.Dwell, context with { Reason = "no heating or cooling conditions were met" }),
      cancellationToken
    );

    return RunMode.Off;
  }

  private float GetHysteresis(float? thermostatHysteresis)
  {
    if (thermostatHysteresis is not float hysteresis)
    {
      logger.LogInformation("Thermostat hysteresis is not configured, using default value of {DefaultHysteresis}C.", defaultHysteresis);
      return defaultHysteresis;
    }

    if (hysteresis < minSafeHysteresis || hysteresis > maxSafeHysteresis)
    {
      logger.LogWarning(
        "Thermostat hysteresis of {ThermostatHysteresis}C is outside the safe range of {MinSafeHysteresis}C to {MaxSafeHysteresis}C. Using default value of {DefaultHysteresis}C.",
        hysteresis,
        minSafeHysteresis,
        maxSafeHysteresis,
        defaultHysteresis
      );
      return defaultHysteresis;
    }

    return hysteresis;
  }

  private static bool IsActive(TimeOnly current, TimeOnly start, TimeOnly end)
  {
    if (start <= end)
      return current >= start && current <= end;

    // Handles schedules spanning midnight
    return current >= start || current <= end;
  }
}
