namespace Kelvin.Server.Models;

/// <summary>
/// Identifies how to decode an Hmi message's body (the content of <see cref="Features.Hmi.HmiEnvelope" />,
/// both directions) - the type/chunk-index/chunk-count/length framing itself lives in the shared envelope,
/// not in any one message's body.
/// </summary>
public enum HmiMessageType : byte
{
  /// <summary>Server -> Hmi. The full thermostat state, possibly spanning multiple envelope chunks; see HmiThermostatStateEncoder.</summary>
  ThermostatStateChunk = 1,

  /// <summary>
  /// Hmi -> Server. The panel's own onboard reading. Body: float TemperatureC, float HumidityPercentage,
  /// ushort CO2LevelPpm, float BatteryLevelPercentage - all little-endian, tightly packed (14 bytes).
  /// </summary>
  SensorReading = 2,

  /// <summary>
  /// Server -> Hmi. The live HVAC call state (only ever a Call-kind <see cref="ControlState" /> change -
  /// Dwell/Heating/Cooling); see HmiControlStateEncoder. Never needs more than one envelope chunk. Body:
  /// byte ControlState, byte hasEnvironmentTemperature, [float environmentTemperatureC], byte hasTargetTemperature,
  /// [float targetTemperatureC], byte hasHumidity, [float humidityPercentage], byte hasCO2, [float co2LevelPpm].
  /// The environment/humidity/CO2 values are the system-wide average across every sensor, not any one
  /// sensor's raw reading - the panel has no need for (and does not receive) individual sensor values.
  /// </summary>
  ControlStateChanged = 3,

  /// <summary>
  /// Server -> Hmi. The same system-wide average reading pushed to web clients via
  /// <see cref="Hubs.RealtimeHub" />'s ReadingsUpdated message - fires far more often than
  /// ControlStateChanged (every sensor packet, not just on an HVAC state transition), so this is the panel's
  /// real-time source for the current reading. See HmiEnvironmentReadingEncoder. Body: float temperatureC,
  /// float humidityPercentage, float co2LevelPpm - all little-endian, tightly packed (12 bytes).
  /// </summary>
  EnvironmentReadingChanged = 4,


  /// <summary>Hmi -> Server. Body: byte RunMode.</summary>
  SetMode = 0x10,

  /// <summary>Hmi -> Server. Body: byte (0/1).</summary>
  SetFanEnabled = 0x11,

  /// <summary>Hmi -> Server. Body: byte RunType, float TargetTemperatureC.</summary>
  SetSetPoint = 0x12,

  /// <summary>Hmi -> Server. Body: byte hasId, [Guid id], byte RunType, ushort startMinutes, ushort endMinutes, float TargetTemperatureC.</summary>
  UpsertSchedule = 0x13,

  /// <summary>Hmi -> Server. Body: Guid id.</summary>
  RemoveSchedule = 0x14,

  /// <summary>Hmi -> Server. Body: byte hasHeating, [float heatingLockoutC], byte hasCooling, [float coolingLockoutC].</summary>
  SetForecastLockouts = 0x15,
}
