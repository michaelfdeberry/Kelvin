#pragma once

#include <stddef.h>
#include <stdint.h>
#include <HmiProtocol.h>

// Builds the wire body (message type byte + payload) for each Hmi -> Server message, matching
// Kelvin.Server's ReceiveHmiCommand.cs decode logic exactly (see HmiProtocol.h for the documented layout
// of each message). Every function returns the number of bytes written into `buffer`, or 0 if `bufferSize`
// was too small for that message.
namespace HmiCommandEncoder
{
  size_t encodeSensorReading(float temperatureC, float humidityPercentage, float batteryLevelPercentage, uint8_t *buffer, size_t bufferSize);

  size_t encodeSetMode(RunMode mode, uint8_t *buffer, size_t bufferSize);

  size_t encodeSetFanEnabled(bool enabled, uint8_t *buffer, size_t bufferSize);

  size_t encodeSetSetPoint(RunType type, float targetTemperatureC, uint8_t *buffer, size_t bufferSize);

  // `scheduleId` is the opaque 16-byte id the server previously sent down for an existing schedule, or
  // nullptr to create a new one.
  size_t encodeUpsertSchedule(
      const uint8_t *scheduleId,
      RunType type,
      uint16_t startMinutes,
      uint16_t endMinutes,
      float targetTemperatureC,
      uint8_t *buffer,
      size_t bufferSize);

  size_t encodeRemoveSchedule(const uint8_t scheduleId[16], uint8_t *buffer, size_t bufferSize);

  // Pass nullptr for either lockout to clear/leave it unset.
  size_t encodeSetForecastLockouts(const float *heatingLockoutC, const float *coolingLockoutC, uint8_t *buffer, size_t bufferSize);
}
