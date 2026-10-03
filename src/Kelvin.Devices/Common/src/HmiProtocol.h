#pragma once

#include <stdint.h>

// Mirrors Kelvin.Server's Models/FrameTags.cs (FrameTags.Hmi) - keep the byte values in sync. Every ESP-NOW
// frame the panel sends or receives (readings AND commands alike) is tagged with this - the Gateway relays
// it verbatim, the server dispatches purely on the tag.
static const uint8_t hmiFrameTag[4] = {0x4B, 0x48, 0x4D, 0x49}; // "KHMI"

// Mirrors Kelvin.Server's Models/HmiMessageType.cs - keep in sync. Identifies how to decode an Hmi
// message's body (the content of the HmiEnvelope below, both directions).
enum class HmiMessageType : uint8_t
{
  // Server -> Hmi. The full thermostat state, possibly spanning multiple envelope chunks.
  // Body: [chunk bytes of the encoding documented below].
  ThermostatStateChunk = 1,

  // Hmi -> Server. The panel's own onboard reading.
  // Body: float temperatureC, float humidityPercentage, uint16 co2LevelPpm (always 0 - no CO2 sensor),
  // float batteryLevelPercentage - all little-endian, tightly packed (14 bytes).
  SensorReading = 2,

  // Server -> Hmi. The live HVAC call state (only ever a Call-kind ControlState change - Dwell/Heating/
  // Cooling), for the HEATING/COOLING badge. Never needs more than one envelope chunk.
  // Body: uint8 ControlState, uint8 hasEnvironmentTemperature, [float environmentTemperatureC],
  // uint8 hasTargetTemperature, [float targetTemperatureC], uint8 hasHumidity, [float humidityPercentage],
  // uint8 hasCO2, [float co2LevelPpm]. The environment/humidity/CO2 values are the system-wide average
  // across every sensor, not any one sensor's raw reading - the panel has no need for (and does not
  // receive) individual sensor values.
  ControlStateChanged = 3,

  // Server -> Hmi. The same system-wide average reading pushed to web clients via EnvironmentReadingsHub -
  // fires far more often than ControlStateChanged (every sensor packet, not just on an HVAC state
  // transition), so this is the panel's real-time source for the current reading.
  // Body: float temperatureC, float humidityPercentage, float co2LevelPpm - all little-endian, tightly
  // packed (12 bytes).
  EnvironmentReadingChanged = 4,

  // Hmi -> Server. Body: uint8 RunMode.
  SetMode = 0x10,

  // Hmi -> Server. Body: uint8 (0/1).
  SetFanEnabled = 0x11,

  // Hmi -> Server. Body: uint8 RunType, float targetTemperatureC.
  SetSetPoint = 0x12,

  // Hmi -> Server. Body: uint8 hasId, [16 bytes id], uint8 RunType, uint16 startMinutes, uint16 endMinutes,
  // float targetTemperatureC.
  UpsertSchedule = 0x13,

  // Hmi -> Server. Body: 16 bytes id.
  RemoveSchedule = 0x14,

  // Hmi -> Server. Body: uint8 hasHeating, [float heatingLockoutC], uint8 hasCooling, [float coolingLockoutC].
  SetForecastLockouts = 0x15,
};

// Mirrors Kelvin.Server's Features/Hmi/HmiEnvelope.cs - keep in sync. Every Hmi message, both directions,
// is framed with this 5-byte header before its HmiMessageType-specific body: message type, chunk index,
// chunk count, and this frame's body length (little-endian uint16). A chunkCount of 1 means the body is
// complete in this single frame; uplink commands never chunk in practice but still carry the same header
// for protocol symmetry.
namespace HmiEnvelope
{
  static const size_t HEADER_SIZE = 5;

  inline void writeHeader(uint8_t *buffer, HmiMessageType type, uint8_t chunkIndex, uint8_t chunkCount, uint16_t bodyLength)
  {
    buffer[0] = (uint8_t)type;
    buffer[1] = chunkIndex;
    buffer[2] = chunkCount;
    buffer[3] = (uint8_t)(bodyLength & 0xFF);
    buffer[4] = (uint8_t)((bodyLength >> 8) & 0xFF);
  }

  // `frameLength` is the full physical frame (header + body). Returns false if the frame is too short to
  // even hold the header, or shorter than the header's own declared body length.
  inline bool readHeader(const uint8_t *frame, size_t frameLength, HmiMessageType &type, uint8_t &chunkIndex, uint8_t &chunkCount, uint16_t &bodyLength)
  {
    if (frameLength < HEADER_SIZE)
    {
      return false;
    }

    type = (HmiMessageType)frame[0];
    chunkIndex = frame[1];
    chunkCount = frame[2];
    bodyLength = (uint16_t)(frame[3] | (frame[4] << 8));
    return frameLength >= HEADER_SIZE + bodyLength;
  }
}


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

// The full thermostat state, as reassembled from one or more ThermostatStateChunk messages (see
// Kelvin.Server's HmiThermostatStateEncoder.cs for the authoritative encoder):
//   uint8 mode (RunMode), uint8 fanEnabled, float hysteresisC,
//   uint8 hasHeatingLockout, [float heatingLockoutC], uint8 hasCoolingLockout, [float coolingLockoutC],
//   uint8 setPointCount, setPointCount * { uint8 RunType, float targetTemperatureC },
//   uint8 scheduleCount, scheduleCount * {
//     16 bytes id (opaque - echo back verbatim on Upsert/RemoveSchedule),
//     uint8 RunType, uint16 startMinutes, uint16 endMinutes, float targetTemperatureC
//   }
// Reassembly/parsing is left to the Hmi sketch's own state decoder, not this shared header.
