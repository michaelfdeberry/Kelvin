#pragma once

#include <stddef.h>
#include <stdint.h>
#include <HmiProtocol.h>

// The reassembled, structured form of a ControlStateChanged payload (see HmiProtocol.h for the wire
// layout this mirrors). Unlike ThermostatStateChunk this always arrives as a single message - no
// reassembly is needed, so there's no decoder class, just a parse function.
//
// environmentTemperatureC/humidityPercentage/co2LevelPpm are the system-wide average across every sensor,
// not any one sensor's raw reading - the panel has no need for (and does not receive) individual readings.
struct ControlCallState
{
  ControlState state = ControlState::Dwell;
  bool hasEnvironmentTemperature = false;
  float environmentTemperatureC = 0.0f;
  bool hasTargetTemperature = false;
  float targetTemperatureC = 0.0f;
  bool hasHumidity = false;
  float humidityPercentage = 0.0f;
  bool hasCO2 = false;
  float co2LevelPpm = 0.0f;
};

namespace ControlStateParser
{
  // `payload` is everything after the HmiMessageType byte. Returns false if the buffer is truncated.
  bool parse(const uint8_t *payload, size_t payloadLength, ControlCallState &outState);
}
