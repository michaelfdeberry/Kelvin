using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Thermostat;

public record SetPointProjection(Guid? Id, RunType Type, float TargetTemperatureC);

public record ScheduleProjection(Guid? Id, RunType Type, TimeOnly StartTime, TimeOnly EndTime, float TargetTemperatureC);

public record ThermostatProjection(float HysteresisC, IEnumerable<SetPointProjection> SetPoints, IEnumerable<ScheduleProjection> Schedules);

public record ValidateThermostatSafetyRequest(ThermostatProjection Projection) : IRequest;

public static class ValidateThermostatSafetyErrors
{
  public static readonly Error DuplicateSetPointType = new(
    "ValidateThermostatSafety.DuplicateSetPointType",
    "Only one set point per run type is allowed."
  );

  public static readonly Error OverlappingSchedulesSameType = new(
    "ValidateThermostatSafety.OverlappingSchedulesSameType",
    "Schedules of the same run type cannot overlap."
  );

  public static readonly Error UnsafeTargetOverlap = new(
    "ValidateThermostatSafety.UnsafeTargetOverlap",
    "Heating and cooling targets overlap the hysteresis safety band."
  );

  public static readonly Error UnsafeActivationOverlap = new(
    "ValidateThermostatSafety.UnsafeActivationOverlap",
    "Heating and cooling activation temperatures overlap."
  );
}

public class ValidateThermostatSafetyHandler(KelvinContext context) : IHandler<ValidateThermostatSafetyRequest>
{
  private const float DefaultHysteresisC = 0.6f;
  private const float MinSafeHysteresisC = 0.3f;
  private const float MaxSafeHysteresisC = 2.0f;

  public async Task<Result> HandleAsync(ValidateThermostatSafetyRequest request, CancellationToken cancellationToken = default)
  {
    var thermostat = await context.Thermostats.FirstAsync(cancellationToken);

    var setPoints = request.Projection.SetPoints.ToList();
    var schedules = request.Projection.Schedules.ToList();

    if (setPoints.GroupBy(sp => sp.Type).Any(group => group.Count() > 1))
    {
      return Result.Failure(ValidateThermostatSafetyErrors.DuplicateSetPointType);
    }

    var effectiveHysteresis = GetEffectiveHysteresis(request.Projection.HysteresisC);

    var heatingSetPoint = setPoints.FirstOrDefault(sp => sp.Type == RunType.Heating);
    var coolingSetPoint = setPoints.FirstOrDefault(sp => sp.Type == RunType.Cooling);

    if (HasUnsafeTargetOverlap(heatingSetPoint?.TargetTemperatureC, coolingSetPoint?.TargetTemperatureC, effectiveHysteresis))
    {
      return Result.Failure(ValidateThermostatSafetyErrors.UnsafeTargetOverlap);
    }

    var enabledSchedules = schedules;

    if (HasSameTypeScheduleOverlap(enabledSchedules))
    {
      return Result.Failure(ValidateThermostatSafetyErrors.OverlappingSchedulesSameType);
    }

    var heatingSchedules = enabledSchedules.Where(schedule => schedule.Type == RunType.Heating).ToList();
    var coolingSchedules = enabledSchedules.Where(schedule => schedule.Type == RunType.Cooling).ToList();

    foreach (var heatingSchedule in heatingSchedules)
    {
      var effectiveHeatingActivation = thermostat.HeatingLockoutC;

      if (HasUnsafeTargetOverlap(heatingSchedule.TargetTemperatureC, coolingSetPoint?.TargetTemperatureC, effectiveHysteresis))
      {
        return Result.Failure(ValidateThermostatSafetyErrors.UnsafeTargetOverlap);
      }

      if (HasUnsafeActivationOverlap(effectiveHeatingActivation, thermostat.CoolingLockoutC))
      {
        return Result.Failure(ValidateThermostatSafetyErrors.UnsafeActivationOverlap);
      }

      foreach (var coolingSchedule in coolingSchedules)
      {
        if (!Overlaps(heatingSchedule.StartTime, heatingSchedule.EndTime, coolingSchedule.StartTime, coolingSchedule.EndTime))
        {
          continue;
        }

        var effectiveCoolingActivation = thermostat.CoolingLockoutC;

        if (HasUnsafeTargetOverlap(heatingSchedule.TargetTemperatureC, coolingSchedule.TargetTemperatureC, effectiveHysteresis))
        {
          return Result.Failure(ValidateThermostatSafetyErrors.UnsafeTargetOverlap);
        }

        if (HasUnsafeActivationOverlap(effectiveHeatingActivation, effectiveCoolingActivation))
        {
          return Result.Failure(ValidateThermostatSafetyErrors.UnsafeActivationOverlap);
        }
      }
    }

    foreach (var coolingSchedule in coolingSchedules)
    {
      if (HasUnsafeTargetOverlap(heatingSetPoint?.TargetTemperatureC, coolingSchedule.TargetTemperatureC, effectiveHysteresis))
      {
        return Result.Failure(ValidateThermostatSafetyErrors.UnsafeTargetOverlap);
      }

      if (HasUnsafeActivationOverlap(thermostat.HeatingLockoutC, thermostat.CoolingLockoutC))
      {
        return Result.Failure(ValidateThermostatSafetyErrors.UnsafeActivationOverlap);
      }
    }

    return Result.Success();
  }

  private static float GetEffectiveHysteresis(float hysteresis)
  {
    if (hysteresis < MinSafeHysteresisC || hysteresis > MaxSafeHysteresisC)
    {
      return DefaultHysteresisC;
    }

    return hysteresis;
  }

  private static bool HasSameTypeScheduleOverlap(IReadOnlyList<ScheduleProjection> schedules)
  {
    var heatingSchedules = schedules.Where(schedule => schedule.Type == RunType.Heating).ToList();
    var coolingSchedules = schedules.Where(schedule => schedule.Type == RunType.Cooling).ToList();

    return HasOverlapWithinType(heatingSchedules) || HasOverlapWithinType(coolingSchedules);
  }

  private static bool HasOverlapWithinType(IReadOnlyList<ScheduleProjection> schedules)
  {
    for (var i = 0; i < schedules.Count; i++)
    {
      for (var j = i + 1; j < schedules.Count; j++)
      {
        if (Overlaps(schedules[i].StartTime, schedules[i].EndTime, schedules[j].StartTime, schedules[j].EndTime))
        {
          return true;
        }
      }
    }

    return false;
  }

  private static bool HasUnsafeTargetOverlap(float? heatingTargetTemperatureC, float? coolingTargetTemperatureC, float hysteresis)
  {
    if (heatingTargetTemperatureC is null || coolingTargetTemperatureC is null)
    {
      return false;
    }

    return heatingTargetTemperatureC.Value >= coolingTargetTemperatureC.Value - (2 * hysteresis);
  }

  private static bool HasUnsafeActivationOverlap(float? heatingActivationTemperatureC, float? coolingActivationTemperatureC)
  {
    if (heatingActivationTemperatureC is null || coolingActivationTemperatureC is null)
    {
      return false;
    }

    return heatingActivationTemperatureC.Value >= coolingActivationTemperatureC.Value;
  }

  private static bool Overlaps(TimeOnly startA, TimeOnly endA, TimeOnly startB, TimeOnly endB)
  {
    return ToMinuteMap(startA, endA).Intersect(ToMinuteMap(startB, endB)).Any();
  }

  private static IEnumerable<int> ToMinuteMap(TimeOnly start, TimeOnly end)
  {
    var currentMinute = start.Hour * 60 + start.Minute;
    var endMinute = end.Hour * 60 + end.Minute;

    while (true)
    {
      yield return currentMinute;
      if (currentMinute == endMinute)
      {
        break;
      }

      currentMinute = (currentMinute + 1) % (24 * 60);
    }
  }
}

public class ValidateThermostatSafetyRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<ValidateThermostatSafetyRequest>, ValidateThermostatSafetyHandler>();
  }
}
