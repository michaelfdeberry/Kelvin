using Kelvin.Server.Models;

namespace Kelvin.Server.Messaging;

/// <summary>Raised whenever ControlService actuates a relay; carries the persisted change record.</summary>
public record ControlStateChangedEvent(ControlStateChange Change);

/// <summary>Raised whenever SensingService recomputes the system-wide average reading.</summary>
public record EnvironmentReadingChangedEvent(EnvironmentReading Reading);

/// <summary>Raised whenever the thermostat's mode, fan, lockouts, set points or schedules change.</summary>
public record ThermostatConfigChangedEvent();

/// <summary>Raised whenever a sensor is created, updated, enabled, disabled or deleted.</summary>
public record SensorsChangedEvent();

/// <summary>Raised to surface a user-facing notification (toast or banner).</summary>
public record NotificationEvent(Notification Notification);
