using Kelvin.Server.Models;

namespace Kelvin.Server.Tests.TestHelpers;

/// <summary>
/// Factory helpers for building <see cref="Thermostat"/> fixtures used by <c>ThermostatService</c> tests.
/// </summary>
public static class ThermostatFixtures
{
    /// <summary>
    /// The instant <see cref="ThermostatServiceHarness"/> pins its clock to, and that every schedule window built
    /// here is positioned around. Fixing it keeps the schedule-window tests independent of what time of day the
    /// suite happens to run, and it is deliberately mid-day so the +/- windows below never wrap past midnight
    /// unless a fixture is explicitly asking for that.
    /// </summary>
    public static readonly DateTimeOffset Now = new(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static Thermostat CreateThermostat(
        RunMode mode = RunMode.Automatic,
        float? hysteresisC = null,
        float? heatingLockoutC = null,
        float? coolingLockoutC = null,
        IEnumerable<SetPoint>? setPoints = null,
        IEnumerable<Schedule>? schedules = null
    )
    {
        var thermostat = new Thermostat
        {
            Id = Guid.NewGuid(),
            Mode = mode,
            HeatingLockoutC = heatingLockoutC,
            CoolingLockoutC = coolingLockoutC,
            SetPoints = (setPoints ?? []).ToList(),
            Schedules = (schedules ?? []).ToList(),
        };

        if (hysteresisC is not null)
        {
            thermostat.HysteresisC = hysteresisC.Value;
        }

        return thermostat;
    }

    public static SetPoint CreateSetPoint(RunType type, float targetTemperatureC) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = type,
            TargetTemperatureC = targetTemperatureC,
        };

    public static Schedule CreateSchedule(
        RunType type,
        float targetTemperatureC,
        TimeOnly startTime,
        TimeOnly endTime
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = type,
            StartTime = startTime,
            EndTime = endTime,
            TargetTemperatureC = targetTemperatureC,
        };

    /// <summary>
    /// Builds a schedule window that is guaranteed to be currently active, bracketing <see cref="Now"/>.
    /// </summary>
    public static Schedule CreateActiveSchedule(RunType type, float targetTemperatureC)
    {
        var now = TimeOnly.FromDateTime(Now.DateTime);
        return CreateSchedule(type, targetTemperatureC, now.AddHours(-1), now.AddHours(1));
    }

    /// <summary>
    /// Builds a schedule window that wraps past midnight (start is later in the day than end) and is guaranteed to
    /// be currently active. The window covers all but a one-minute slice of the day, so <c>now</c> always falls
    /// inside it regardless of when the test runs, while still exercising the wrap-around branch of the service's
    /// active-window check.
    /// </summary>
    public static Schedule CreateMidnightSpanningActiveSchedule(
        RunType type,
        float targetTemperatureC
    )
    {
        var now = TimeOnly.FromDateTime(Now.DateTime);
        return CreateSchedule(type, targetTemperatureC, now.AddMinutes(-30), now.AddMinutes(-31));
    }

    /// <summary>
    /// Builds a schedule window that is guaranteed to NOT be currently active (fully in the past relative to now).
    /// </summary>
    public static Schedule CreateInactiveSchedule(RunType type, float targetTemperatureC)
    {
        var now = TimeOnly.FromDateTime(Now.DateTime);
        return CreateSchedule(type, targetTemperatureC, now.AddHours(-3), now.AddHours(-2));
    }

    public static Kelvin.Server.Models.EnvironmentReading CreateEnvironment(
        float temperatureC,
        float humidityPercentage = 0f
    ) =>
        new()
        {
            Timestamp = Now,
            TemperatureC = temperatureC,
            HumidityPercentage = humidityPercentage,
        };
}
