namespace Kelvin.Server.Models;

/// <summary>
/// Identifies how to decode an Hmi message's body (the content of <see cref="Features.Hmi.HmiEnvelope" />,
/// both directions) - the type/chunk-index/chunk-count/length framing itself lives in the shared envelope,
/// not in any one message's body. Every body is a versioned CBOR map defined in docs/hmi-payloads.cddl.
/// </summary>
public enum HmiMessageType : byte
{
  /// <summary>Server -> Hmi. The full thermostat state, possibly spanning multiple envelope chunks; see HmiThermostatStateEncoder.</summary>
  ThermostatStateChunk = 1,

  /// <summary>
  /// Hmi -> Server. The panel's own onboard reading (temperature, humidity, CO2, and battery).
  /// </summary>
  SensorReading = 2,

  /// <summary>
  /// Server -> Hmi. The live HVAC call state (only ever a Call-kind <see cref="ControlState" /> change -
  /// Dwell/Heating/Cooling); see HmiControlStateEncoder. Optional readings are explicit CBOR nulls.
  /// The environment/humidity/CO2 values are the system-wide average across every sensor, not any one
  /// sensor's raw reading - the panel has no need for (and does not receive) individual sensor values.
  /// </summary>
  ControlStateChanged = 3,

  /// <summary>
  /// Server -> Hmi. The same system-wide average reading pushed to web clients via
  /// <see cref="Hubs.RealtimeHub" />'s ReadingsUpdated message - fires far more often than
  /// ControlStateChanged (every sensor packet, not just on an HVAC state transition), so this is the panel's
  /// real-time source for the current reading. See HmiEnvironmentReadingEncoder. Body: versioned CBOR
  /// map {0: version, 1: temperatureC, 2: humidityPercentage, 3: co2LevelPpm}; see docs/HMI Protocol.md.
  /// </summary>
  EnvironmentReadingChanged = 4,

  /// <summary>
  /// Server -> Hmi. The system-wide preferences have changed. Body: encoded preferences chunk (see HmiPreferencesEncoder).
  /// </summary>
  PreferencesChanged = 5,

  /// <summary>Hmi -> Server. Sets RunMode.</summary>
  SetMode = 0x10,

  /// <summary>Hmi -> Server. Sets fan enabled using a CBOR boolean.</summary>
  SetFanEnabled = 0x11,

  /// <summary>Hmi -> Server. Sets a run type's target temperature.</summary>
  SetSetPoint = 0x12,

  /// <summary>Hmi -> Server. Upserts a schedule; a null ID creates a new schedule.</summary>
  UpsertSchedule = 0x13,

  /// <summary>Hmi -> Server. Removes a schedule by opaque 16-byte ID.</summary>
  RemoveSchedule = 0x14,

  /// <summary>Hmi -> Server. Sets nullable heating/cooling forecast lockouts.</summary>
  SetForecastLockouts = 0x15,

  /// <summary>Hmi -> Server. Requests the last call state and preferences, replied to this panel only.</summary>
  RequestState = 0x16,
}
