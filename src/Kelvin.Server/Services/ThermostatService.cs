using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.GeoCoding;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Features.Weather;
using Kelvin.Server.Models;

namespace Kelvin.Server.Services;

public class ThermostatService(
  IControlChannel controlChannel,
  IEnvironmentReadingsChannel environmentChannel,
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
          State: _activeCall,
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
            new ControlMessage(context with { State = ControlState.Disable, Reason = "the thermostat mode is Disabled" }),
            stoppingToken
          );
          continue;
        }

        // re-assert control every cycle so a restart re-energizes the control relay, noop if already energized.
        await controlChannel.WriteAsync(
          new ControlMessage(context with { State = ControlState.Enable, Reason = "the thermostat is enabled" }),
          stoppingToken
        );

        if (thermostat.Mode == RunMode.Off)
        {
          logger.LogInformation("Thermostat is Off, skipping environment processing.");
          _activeCall = ControlState.Dwell;
          await controlChannel.WriteAsync(
            new ControlMessage(context with { State = ControlState.Dwell, Reason = "the thermostat mode is Off" }),
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
        var runMode = await ProcessTemperature(context, environment, thermostat, forecastTemperatureC, stoppingToken);
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

  private ControlContext ProcessAutomatic(ControlContext context, EnvironmentReading environment, Thermostat thermostat, float? forecastTemperatureC)
  {
    var currentTimeOnly = TimeOnly.FromDateTime(time.GetLocalNow().DateTime);
    var heatingSetPoint = thermostat.SetPoints.FirstOrDefault(sp => sp.Type == RunType.Heating);
    var coolingSetPoint = thermostat.SetPoints.FirstOrDefault(sp => sp.Type == RunType.Cooling);
    var heatingSchedule = thermostat.Schedules.FirstOrDefault(s => s.Type == RunType.Heating && IsActive(currentTimeOnly, s.StartTime, s.EndTime));
    var coolingSchedule = thermostat.Schedules.FirstOrDefault(s => s.Type == RunType.Cooling && IsActive(currentTimeOnly, s.StartTime, s.EndTime));
    var heatingTargetTemp = heatingSchedule?.TargetTemperatureC ?? heatingSetPoint?.TargetTemperatureC;
    var coolingTargetTemp = coolingSchedule?.TargetTemperatureC ?? coolingSetPoint?.TargetTemperatureC;

    // this shouldn't be possible, it's not allowed to not have both configured for auto mode
    if (heatingTargetTemp is null || coolingTargetTemp is null)
    {
      logger.LogInformation("Invalid configuration for automatic control.");
      return context with { State = ControlState.Dwell, Reason = "Invalid configuration for automatic control" };
    }

    var isHeatingMode = false;
    var isCoolingMode = false;
    var hysteresis = GetHysteresis(thermostat.HysteresisC);
    var hysteresisRange = 2 * hysteresis;
    var forecastRange = 5;

    // no forecast integration at all, both lockouts are required if forecast is used in automatic mode.
    if (forecastTemperatureC is null || thermostat.HeatingLockoutC is null || thermostat.CoolingLockoutC is null)
    {
      var hasInvalidTargets = heatingTargetTemp > coolingTargetTemp;
      var hasInvalidHysteresis = coolingTargetTemp - heatingTargetTemp < hysteresisRange;
      if (hasInvalidTargets || hasInvalidHysteresis)
      {
        logger.LogCritical(
          @"
            The heating target temperature ({HeatingTargetTemp}C) is higher than the cooling target temperature ({CoolingTargetTemp}C). 
            This is an invalid configuration.
          ",
          heatingTargetTemp,
          coolingTargetTemp
        );
        return context with { State = ControlState.Disable, Reason = "the heating target temperature is higher than the cooling target temperature" };
      }

      isHeatingMode = heatingTargetTemp is not null && environment.TemperatureC <= (heatingTargetTemp - hysteresis);
      isCoolingMode = coolingTargetTemp is not null && environment.TemperatureC >= (coolingTargetTemp + hysteresis);
    }
    else
    {
      // Cooling has to be greater than heating, and the forecast temperature has to be within the range of the two targets,
      // otherwise it is an invalid configuration.
      var invalidForecastRange = thermostat.CoolingLockoutC - thermostat.HeatingLockoutC < forecastRange;
      if (invalidForecastRange)
      {
        logger.LogWarning(
          @"
            The forecast temperature ({ForecastTemperatureC}C) is outside the valid range of the heating target temperature ({HeatingTargetTemp}C) and cooling target temperature ({CoolingTargetTemp}C). 
            This is an invalid configuration.
          ",
          forecastTemperatureC,
          heatingTargetTemp,
          coolingTargetTemp
        );

        return context with
        {
          State = ControlState.Dwell,
          Reason = "the forecast temperature is outside the valid range of the heating and cooling target temperatures",
        };
      }
      isHeatingMode = heatingTargetTemp is not null && forecastTemperatureC <= thermostat.HeatingLockoutC;
      isCoolingMode = coolingTargetTemp is not null && forecastTemperatureC >= thermostat.CoolingLockoutC;
    }

    // shouldn't be possible given the above logic, but just in case, log a warning and return Dwell if both heating and cooling conditions are met.
    if (isHeatingMode && isCoolingMode)
    {
      logger.LogCritical(
        "Both heating and cooling conditions are met. This is an unexpected state. Forecast temperature: {ForecastTemperatureC}C, Heating target: {HeatingTargetTemp}C, Cooling target: {CoolingTargetTemp}C",
        forecastTemperatureC,
        heatingTargetTemp,
        coolingTargetTemp
      );
      return context with { State = ControlState.Disable, Reason = "both heating and cooling conditions were met" };
    }

    if (!isHeatingMode && !isCoolingMode)
    {
      logger.LogInformation("No active heating or cooling mode found.");
      return context with { State = ControlState.Dwell, Reason = "no active heating or cooling mode is active" };
    }

    if (isHeatingMode)
    {
      return ProcessHeating(context, environment, thermostat, forecastTemperatureC);
    }

    if (isCoolingMode)
    {
      return ProcessCooling(context, environment, thermostat, forecastTemperatureC);
    }

    return context with
    {
      State = ControlState.Dwell,
      Reason = "no active heating or cooling mode",
    };
  }

  private ControlContext ProcessCooling(ControlContext context, EnvironmentReading environment, Thermostat thermostat, float? forecastTemperatureC)
  {
    context = context with { ForecastTemperatureC = forecastTemperatureC };

    var currentTimeOnly = TimeOnly.FromDateTime(time.GetLocalNow().DateTime);
    var environmentTemperatureC = environment.TemperatureC;
    var coolingLockoutC = thermostat.CoolingLockoutC;
    var coolingSetPoint = thermostat.SetPoints.FirstOrDefault(sp => sp.Type == RunType.Cooling);
    var coolingSchedule = thermostat.Schedules.FirstOrDefault(s => s.Type == RunType.Cooling && IsActive(currentTimeOnly, s.StartTime, s.EndTime));
    var useForecastForCooling = forecastTemperatureC is not null && coolingLockoutC is not null;
    var forecastAllowsForCooling = useForecastForCooling && forecastTemperatureC >= coolingLockoutC;

    if (coolingSetPoint is null && coolingSchedule is null)
    {
      logger.LogInformation("No active cooling schedule or set point found.");
      return context with { State = ControlState.Dwell, Reason = "no cooling schedule or set point is active" };
    }

    var callForCooling = false;
    var hysteresis = GetHysteresis(thermostat.HysteresisC);
    var coolingTargetTemp = coolingSchedule?.TargetTemperatureC ?? coolingSetPoint?.TargetTemperatureC;
    var shouldCheckForCooling = (useForecastForCooling && forecastAllowsForCooling) || !useForecastForCooling;

    if (shouldCheckForCooling)
    {
      if (_activeCall == ControlState.Dwell && environmentTemperatureC >= (coolingTargetTemp + hysteresis))
      {
        callForCooling = true;
      }
      else if (_activeCall == ControlState.Cooling && environmentTemperatureC > (coolingTargetTemp - hysteresis))
      {
        // all ready cooling, calling for cool is a noop, but we will still return a control message to keep the state updated.
        callForCooling = true;
      }
    }

    if (callForCooling && (thermostat.Mode == RunMode.Cooling || thermostat.Mode == RunMode.Automatic))
    {
      logger.LogInformation("Thermostat is in cooling mode and conditions are met for cooling.");
      return context with
      {
        State = ControlState.Cooling,
        TargetTemperatureC = coolingTargetTemp,
        ScheduleId = coolingSchedule?.Id,
        SetPointId = coolingSchedule is null ? coolingSetPoint?.Id : null,
        Reason = "the cooling conditions were met",
      };
    }

    logger.LogInformation("No conditions are met for cooling, turning off the system.");
    return context with
    {
      State = ControlState.Dwell,
      TargetTemperatureC = coolingTargetTemp,
      ScheduleId = coolingSchedule?.Id,
      SetPointId = coolingSchedule is null ? coolingSetPoint?.Id : null,
      Reason = "no cooling conditions were met",
    };
  }

  private ControlContext ProcessHeating(ControlContext context, EnvironmentReading environment, Thermostat thermostat, float? forecastTemperatureC)
  {
    context = context with { ForecastTemperatureC = forecastTemperatureC };

    var currentTimeOnly = TimeOnly.FromDateTime(time.GetLocalNow().DateTime);
    var environmentTemperatureC = environment.TemperatureC;
    var heatingLockoutC = thermostat.HeatingLockoutC;
    var heatingSetPoint = thermostat.SetPoints.FirstOrDefault(sp => sp.Type == RunType.Heating);
    var heatingSchedule = thermostat.Schedules.FirstOrDefault(s => s.Type == RunType.Heating && IsActive(currentTimeOnly, s.StartTime, s.EndTime));
    var useForecastForHeating = forecastTemperatureC is not null && heatingLockoutC is not null;
    var forecastAllowsForHeating = useForecastForHeating && forecastTemperatureC <= heatingLockoutC;

    if (heatingSetPoint is null && heatingSchedule is null)
    {
      logger.LogInformation("No active heating schedule or set point found.");
      return context with { State = ControlState.Dwell, Reason = "no heating schedule or set point is active" };
    }

    var callForHeating = false;
    var hysteresis = GetHysteresis(thermostat.HysteresisC);
    var heatingTargetTemp = heatingSchedule?.TargetTemperatureC ?? heatingSetPoint?.TargetTemperatureC;
    var shouldCheckForHeating = (useForecastForHeating && forecastAllowsForHeating) || !useForecastForHeating;

    if (shouldCheckForHeating)
    {
      if (_activeCall == ControlState.Dwell && environmentTemperatureC <= (heatingTargetTemp - hysteresis))
      {
        callForHeating = true;
      }
      else if (_activeCall == ControlState.Heating && environmentTemperatureC < (heatingTargetTemp + hysteresis))
      {
        // all ready heating, calling for heat is a noop, but we will still return a control message to keep the state updated.
        callForHeating = true;
      }
    }

    if (callForHeating && (thermostat.Mode == RunMode.Heating || thermostat.Mode == RunMode.Automatic))
    {
      logger.LogInformation("Thermostat is in heating mode and conditions are met for heating.");
      return context with
      {
        State = ControlState.Heating,
        TargetTemperatureC = heatingTargetTemp,
        ScheduleId = heatingSchedule?.Id,
        SetPointId = heatingSchedule is null ? heatingSetPoint?.Id : null,
        Reason = "the heating conditions were met",
      };
    }

    logger.LogInformation("No conditions are met for heating, turning off the system.");
    return context with
    {
      State = ControlState.Dwell,
      TargetTemperatureC = heatingTargetTemp,
      ScheduleId = heatingSchedule?.Id,
      SetPointId = heatingSchedule is null ? heatingSetPoint?.Id : null,
      Reason = "no heating conditions were met",
    };
  }

  private async Task<ControlContext> ProcessTemperature(
    ControlContext context,
    EnvironmentReading environment,
    Thermostat thermostat,
    float? forecastTemperatureC,
    CancellationToken cancellationToken
  )
  {
    context = thermostat.Mode switch
    {
      RunMode.Heating => ProcessHeating(context, environment, thermostat, forecastTemperatureC),
      RunMode.Cooling => ProcessCooling(context, environment, thermostat, forecastTemperatureC),
      RunMode.Automatic => ProcessAutomatic(context, environment, thermostat, forecastTemperatureC),
      _ => context with { State = ControlState.Dwell, Reason = "the thermostat mode is Off or Disabled" },
    };

    _activeCall = context.State;
    await controlChannel.WriteAsync(new ControlMessage(context), cancellationToken);
    return context;
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
