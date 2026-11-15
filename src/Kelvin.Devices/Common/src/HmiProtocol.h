#pragma once

#include <stddef.h>
#include <stdint.h>
#include "DeviceEnvelope.h"

// Mirrors Kelvin.Server's Models/FrameTags.cs (FrameTags.Hmi) - keep the byte values in sync. Every ESP-NOW
// frame the panel sends or receives (readings AND commands alike) is tagged with this - the Gateway relays
// it verbatim, the server dispatches purely on the tag.
static const uint8_t hmiFrameTag[4] = {0x4B, 0x48, 0x4D, 0x49}; // "KHMI"

// Mirrors Kelvin.Server's Models/HmiMessageType.cs - keep in sync. Identifies how to decode an Hmi
// message's body (the content of the DeviceEnvelope, both directions). Every body is a versioned
// CBOR map; field definitions live in docs/hmi-payloads.cddl and docs/HMI Protocol.md.
enum class HmiMessageType : uint8_t
{
  // Server -> Hmi. The full thermostat state, possibly spanning multiple envelope chunks.
  ThermostatStateChunk = 1,

  // Hmi -> Server. The panel's own onboard reading.
  SensorReading = 2,

  // Server -> Hmi. The live HVAC call state (only ever a Call-kind ControlState change - Dwell/Heating/
  // Cooling), for the HEATING/COOLING badge. Never needs more than one envelope chunk.
  // The environment/humidity/CO2 values are the system-wide average
  // across every sensor, not any one sensor's raw reading - the panel has no need for (and does not
  // receive) individual sensor values.
  ControlStateChanged = 3,

  // Server -> Hmi. The same system-wide average reading pushed to web clients via EnvironmentReadingsHub -
  // fires far more often than ControlStateChanged (every sensor packet, not just on an HVAC state
  // transition), so this is the panel's real-time source for the current reading.
  // Body: CBOR map {0: version(1), 1: temperatureC, 2: humidityPercentage, 3: co2LevelPpm}.
  EnvironmentReadingChanged = 4,

  // Server -> Hmi. The system's preferences have changed.
  PreferencesChanged = 5,
  Notify = 6,
  DismissNotification = 7,

  // Hmi -> Server. Sets RunMode.
  SetMode = 0x10,

  // Hmi -> Server. Sets fan enabled.
  SetFanEnabled = 0x11,

  // Hmi -> Server. Sets a run type's target temperature.
  SetSetPoint = 0x12,

  // Hmi -> Server. Upserts a schedule; a null ID creates a new schedule.
  UpsertSchedule = 0x13,

  // Hmi -> Server. Removes a schedule by opaque 16-byte ID.
  RemoveSchedule = 0x14,

  // Hmi -> Server. Sets nullable heating/cooling forecast lockouts.
  SetForecastLockouts = 0x15,

  // Hmi -> Server. Requests the last call state and preferences, replied to this panel only.
  RequestState = 0x16,
};

// Mirrors Kelvin.Server's Models/RunMode.cs ordinal values - keep in sync.
enum class RunMode : uint8_t
{
  Disabled = 0,
  Off = 1,
  Heating = 2,
  Cooling = 3,
  Automatic = 4,
};

// Mirrors Kelvin.Server's Models/RunType.cs ordinal values - keep in sync.
enum class RunType : uint8_t
{
  Heating = 0,
  Cooling = 1,
};

// Mirrors Kelvin.Server's Models/ControlMessage.cs ControlState enum ordinals - keep in sync. Only
// Dwell/Heating/Cooling are ever sent in a ControlStateChanged message (Kind=Call changes).
enum class ControlState : uint8_t
{
  Disable = 0,
  Enable = 1,
  Dwell = 2,
  Heating = 3,
  Cooling = 4,
  FanOn = 5,
  FanOff = 6,
  Startup = 7,
  Fault = 8,
};

enum class TemperatureUnit : uint8_t
{
  Celsius = 0,
  Fahrenheit = 1,
};

enum class TimeFormat : uint8_t
{
  Hour24 = 0,
  Hour12 = 1,
};

// Reassembly/parsing is left to the Hmi sketch's own state decoder, not this shared header.
