namespace Kelvin.Server.Models;

/// <summary>
/// The first byte of every Hmi message payload (both directions), identifying how to decode the rest.
/// </summary>
public enum HmiMessageType : byte
{
  /// <summary>Server -> Hmi. One chunk of the full thermostat state; see HmiThermostatStateEncoder.</summary>
  ThermostatStateChunk = 1,

  /// <summary>
  /// Hmi -> Server. The panel's own onboard reading. Body: float TemperatureC, float HumidityPercentage,
  /// ushort CO2LevelPpm, float BatteryLevelPercentage - all little-endian, tightly packed (14 bytes).
  /// </summary>
  SensorReading = 2,

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
