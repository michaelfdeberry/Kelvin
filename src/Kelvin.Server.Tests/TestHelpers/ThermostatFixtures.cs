using Kelvin.Server.Models;

namespace Kelvin.Server.Tests.TestHelpers;

/// <summary>
/// Factory helpers for building <see cref="Thermostat"/> fixtures used by <c>ThermostatService</c> tests.
/// </summary>
public static class ThermostatFixtures
{
    public static Thermostat CreateThermostat(
        RunMode mode = RunMode.Automatic,
        float? hysteresisC = null,
        IEnumerable<SetPoint>? setPoints = null,
        IEnumerable<Schedule>? schedules = null
    )
    {
        var thermostat = new Thermostat
        {
            Id = Guid.NewGuid(),
            Mode = mode,
            SetPoints = (setPoints ?? []).ToList(),
            Schedules = (schedules ?? []).ToList(),
        };

        if (hysteresisC is not null)
        {
            thermostat.HysteresisC = hysteresisC.Value;
        }

        return thermostat;
    }

    public static SetPoint CreateSetPoint(
        RunType type,
        float targetTemperatureC,
        float? activationTemperatureC = null
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = type,
            TargetTemperatureC = targetTemperatureC,
            ActivationTemperatureC = activationTemperatureC,
        };

    public static Schedule CreateSchedule(
        RunType type,
        float targetTemperatureC,
        TimeOnly startTime,
        TimeOnly endTime,
        bool enabled = true,
        float? activationTemperatureC = null
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = type,
            Enabled = enabled,
            StartTime = startTime,
            EndTime = endTime,
            TargetTemperatureC = targetTemperatureC,
            ActivationTemperatureC = activationTemperatureC,
        };

    /// <summary>
    /// Builds a schedule window that is guaranteed to be currently active, bracketing <see cref="DateTimeOffset.Now"/>.
    /// </summary>
    public static Schedule CreateActiveSchedule(
        RunType type,
        float targetTemperatureC,
        bool enabled = true,
        float? activationTemperatureC = null
    )
    {
        var now = TimeOnly.FromDateTime(DateTimeOffset.Now.DateTime);
        return CreateSchedule(
            type,
            targetTemperatureC,
            now.AddHours(-1),
            now.AddHours(1),
            enabled,
            activationTemperatureC
        );
    }

    /// <summary>
    /// Builds a schedule window that wraps past midnight (start is later in the day than end) and is guaranteed to
    /// be currently active. The window covers all but a one-minute slice of the day, so <c>now</c> always falls
    /// inside it regardless of when the test runs, while still exercising the wrap-around branch of the service's
    /// active-window check.
    /// </summary>
    public static Schedule CreateMidnightSpanningActiveSchedule(
        RunType type,
        float targetTemperatureC,
        bool enabled = true,
        float? activationTemperatureC = null
    )
    {
        var now = TimeOnly.FromDateTime(DateTimeOffset.Now.DateTime);
        return CreateSchedule(
            type,
            targetTemperatureC,
            now.AddMinutes(-30),
            now.AddMinutes(-31),
            enabled,
            activationTemperatureC
        );
    }

    /// <summary>
    /// Builds a schedule window that is guaranteed to NOT be currently active (fully in the past relative to now).
    /// </summary>
    public static Schedule CreateInactiveSchedule(
        RunType type,
        float targetTemperatureC,
        bool enabled = true,
        float? activationTemperatureC = null
    )
    {
        var now = TimeOnly.FromDateTime(DateTimeOffset.Now.DateTime);
        return CreateSchedule(
            type,
            targetTemperatureC,
            now.AddHours(-3),
            now.AddHours(-2),
            enabled,
            activationTemperatureC
        );
    }

    public static Kelvin.Server.Models.Environment CreateEnvironment(double temperatureC) =>
        new() { Timestamp = DateTimeOffset.UtcNow, TemperatureC = temperatureC };
}
