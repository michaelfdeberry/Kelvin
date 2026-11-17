using Kelvin.Server.Features.Gateways;
using Kelvin.Server.Models;

namespace Kelvin.Server.Messaging;

/// <summary>Raised whenever ControlService actuates a relay; carries the persisted change record.</summary>
public record ControlStateChangedEvent(ControlStateChange Change);

/// <summary>Raised whenever SensingService recomputes the system-wide average reading.</summary>
public record EnvironmentReadingChangedEvent(EnvironmentReading Reading);

/// <summary>Raised whenever the thermostat's mode, fan, lockouts, set points or schedules change; carries the saved thermostat with its set points and schedules.</summary>
public record ThermostatConfigChangedEvent(Models.Thermostat Thermostat);

/// <summary>Raised whenever a sensor is created, updated, enabled, disabled or restored; carries the saved sensor.</summary>
public record SensorChangedEvent(Sensor Sensor);

/// <summary>Raised whenever a sensor is deleted.</summary>
public record SensorDeletedEvent(Guid SensorId);

/// <summary>Raised whenever a device is created, updated, enabled, disabled or restored, its configuration changes, or it checks in; carries the saved device.</summary>
public record DeviceChangedEvent(Device Device);

/// <summary>Raised after a device is deleted, so its type can clean up after it (e.g. switch off what it was running), and clients can drop it.</summary>
public record DeviceDeletedEvent(Device Device);

/// <summary>Raised after a device is disabled, so its type can stop what it was running.</summary>
public record DeviceDisabledEvent(Device Device);

/// <summary>Raised after the RelayController moves any HVAC relay; carries the state of every relay.</summary>
public record RelayStatesChangedEvent(GetRelayStatesResponse States);

/// <summary>Raised whenever a device's reported state changes; carries the latest state of every device type by name.</summary>
public record DeviceStateChangedEvent(IReadOnlyDictionary<string, object> States);

/// <summary>Raised to surface a user-facing notification (toast or banner).</summary>
public record SendNotificationEvent(Notification Notification);

/// <summary>Raised whenever a user dismisses a notification.</summary>
public record DismissNotificationEvent(Guid NotificationId);

/// <summary>
/// Raised whenever the system preferences are changed.
/// </summary>
public record PreferencesChangedEvent(Preferences Preferences);
