#pragma once

#include <stddef.h>
#include <stdint.h>

// The system-wide average reading (see HmiProtocol.h for the wire layout this mirrors) - fires far more
// often than a ControlStateChanged push, so this is what keeps the panel's current reading live in between.
struct EnvironmentAverageReading
{
  float temperatureC = 0.0f;
  float humidityPercentage = 0.0f;
  float co2LevelPpm = 0.0f;
};

namespace EnvironmentReadingParser
{
  // `payload` is everything after the HmiMessageType byte. Returns false if the buffer is truncated.
  bool parse(const uint8_t *payload, size_t payloadLength, EnvironmentAverageReading &outReading);
}
