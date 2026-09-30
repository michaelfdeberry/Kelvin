#include <string.h>
#include "HmiCommandEncoder.h"

namespace HmiCommandEncoder
{
  size_t encodeSensorReading(float temperatureC, float humidityPercentage, float batteryLevelPercentage, uint8_t *buffer, size_t bufferSize)
  {
    const size_t required = 15; // type + temp(4) + humidity(4) + co2(2) + battery(4)
    if (bufferSize < required)
    {
      return 0;
    }

    size_t offset = 0;
    buffer[offset++] = (uint8_t)HmiMessageType::SensorReading;
    memcpy(buffer + offset, &temperatureC, sizeof(float));
    offset += sizeof(float);
    memcpy(buffer + offset, &humidityPercentage, sizeof(float));
    offset += sizeof(float);
    uint16_t co2LevelPpm = 0; // this panel has no CO2 sensor
    memcpy(buffer + offset, &co2LevelPpm, sizeof(uint16_t));
    offset += sizeof(uint16_t);
    memcpy(buffer + offset, &batteryLevelPercentage, sizeof(float));
    offset += sizeof(float);
    return offset;
  }

  size_t encodeSetMode(RunMode mode, uint8_t *buffer, size_t bufferSize)
  {
    if (bufferSize < 2)
    {
      return 0;
    }

    buffer[0] = (uint8_t)HmiMessageType::SetMode;
    buffer[1] = (uint8_t)mode;
    return 2;
  }

  size_t encodeSetFanEnabled(bool enabled, uint8_t *buffer, size_t bufferSize)
  {
    if (bufferSize < 2)
    {
      return 0;
    }

    buffer[0] = (uint8_t)HmiMessageType::SetFanEnabled;
    buffer[1] = enabled ? 1 : 0;
    return 2;
  }

  size_t encodeSetSetPoint(RunType type, float targetTemperatureC, uint8_t *buffer, size_t bufferSize)
  {
    const size_t required = 6; // type + RunType(1) + target(4)
    if (bufferSize < required)
    {
      return 0;
    }

    buffer[0] = (uint8_t)HmiMessageType::SetSetPoint;
    buffer[1] = (uint8_t)type;
    memcpy(buffer + 2, &targetTemperatureC, sizeof(float));
    return required;
  }

  size_t encodeUpsertSchedule(
      const uint8_t *scheduleId,
      RunType type,
      uint16_t startMinutes,
      uint16_t endMinutes,
      float targetTemperatureC,
      uint8_t *buffer,
      size_t bufferSize)
  {
    const size_t required = scheduleId ? 27 : 11; // type + hasId + [16] + RunType + start(2) + end(2) + target(4)
    if (bufferSize < required)
    {
      return 0;
    }

    size_t offset = 0;
    buffer[offset++] = (uint8_t)HmiMessageType::UpsertSchedule;
    buffer[offset++] = scheduleId ? 1 : 0;
    if (scheduleId)
    {
      memcpy(buffer + offset, scheduleId, 16);
      offset += 16;
    }
    buffer[offset++] = (uint8_t)type;
    memcpy(buffer + offset, &startMinutes, sizeof(uint16_t));
    offset += sizeof(uint16_t);
    memcpy(buffer + offset, &endMinutes, sizeof(uint16_t));
    offset += sizeof(uint16_t);
    memcpy(buffer + offset, &targetTemperatureC, sizeof(float));
    offset += sizeof(float);
    return offset;
  }

  size_t encodeRemoveSchedule(const uint8_t scheduleId[16], uint8_t *buffer, size_t bufferSize)
  {
    const size_t required = 17; // type + id(16)
    if (bufferSize < required)
    {
      return 0;
    }

    buffer[0] = (uint8_t)HmiMessageType::RemoveSchedule;
    memcpy(buffer + 1, scheduleId, 16);
    return required;
  }

  size_t encodeSetForecastLockouts(const float *heatingLockoutC, const float *coolingLockoutC, uint8_t *buffer, size_t bufferSize)
  {
    const size_t required = 2 + (heatingLockoutC ? sizeof(float) : 0) + (coolingLockoutC ? sizeof(float) : 0);
    if (bufferSize < required)
    {
      return 0;
    }

    size_t offset = 0;
    buffer[offset++] = (uint8_t)HmiMessageType::SetForecastLockouts;
    buffer[offset++] = heatingLockoutC ? 1 : 0;
    if (heatingLockoutC)
    {
      memcpy(buffer + offset, heatingLockoutC, sizeof(float));
      offset += sizeof(float);
    }
    buffer[offset++] = coolingLockoutC ? 1 : 0;
    if (coolingLockoutC)
    {
      memcpy(buffer + offset, coolingLockoutC, sizeof(float));
      offset += sizeof(float);
    }
    return offset;
  }
}
