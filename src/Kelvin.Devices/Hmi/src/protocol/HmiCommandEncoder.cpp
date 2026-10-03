#include <string.h>
#include "HmiCommandEncoder.h"

namespace HmiCommandEncoder
{
  size_t encodeSensorReading(float temperatureC, float humidityPercentage, float batteryLevelPercentage, uint8_t *buffer, size_t bufferSize)
  {
    const uint16_t bodyLength = 14; // temp(4) + humidity(4) + co2(2) + battery(4)
    if (bufferSize < HmiEnvelope::HEADER_SIZE + bodyLength)
    {
      return 0;
    }

    HmiEnvelope::writeHeader(buffer, HmiMessageType::SensorReading, 0, 1, bodyLength);
    size_t offset = HmiEnvelope::HEADER_SIZE;
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
    const uint16_t bodyLength = 1;
    if (bufferSize < HmiEnvelope::HEADER_SIZE + bodyLength)
    {
      return 0;
    }

    HmiEnvelope::writeHeader(buffer, HmiMessageType::SetMode, 0, 1, bodyLength);
    buffer[HmiEnvelope::HEADER_SIZE] = (uint8_t)mode;
    return HmiEnvelope::HEADER_SIZE + bodyLength;
  }

  size_t encodeSetFanEnabled(bool enabled, uint8_t *buffer, size_t bufferSize)
  {
    const uint16_t bodyLength = 1;
    if (bufferSize < HmiEnvelope::HEADER_SIZE + bodyLength)
    {
      return 0;
    }

    HmiEnvelope::writeHeader(buffer, HmiMessageType::SetFanEnabled, 0, 1, bodyLength);
    buffer[HmiEnvelope::HEADER_SIZE] = enabled ? 1 : 0;
    return HmiEnvelope::HEADER_SIZE + bodyLength;
  }

  size_t encodeSetSetPoint(RunType type, float targetTemperatureC, uint8_t *buffer, size_t bufferSize)
  {
    const uint16_t bodyLength = 5; // RunType(1) + target(4)
    if (bufferSize < HmiEnvelope::HEADER_SIZE + bodyLength)
    {
      return 0;
    }

    HmiEnvelope::writeHeader(buffer, HmiMessageType::SetSetPoint, 0, 1, bodyLength);
    size_t offset = HmiEnvelope::HEADER_SIZE;
    buffer[offset++] = (uint8_t)type;
    memcpy(buffer + offset, &targetTemperatureC, sizeof(float));
    return HmiEnvelope::HEADER_SIZE + bodyLength;
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
    const uint16_t bodyLength = scheduleId ? 26 : 10; // hasId + [16] + RunType(1) + start(2) + end(2) + target(4)
    if (bufferSize < HmiEnvelope::HEADER_SIZE + bodyLength)
    {
      return 0;
    }

    HmiEnvelope::writeHeader(buffer, HmiMessageType::UpsertSchedule, 0, 1, bodyLength);
    size_t offset = HmiEnvelope::HEADER_SIZE;
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
    const uint16_t bodyLength = 16;
    if (bufferSize < HmiEnvelope::HEADER_SIZE + bodyLength)
    {
      return 0;
    }

    HmiEnvelope::writeHeader(buffer, HmiMessageType::RemoveSchedule, 0, 1, bodyLength);
    memcpy(buffer + HmiEnvelope::HEADER_SIZE, scheduleId, 16);
    return HmiEnvelope::HEADER_SIZE + bodyLength;
  }

  size_t encodeSetForecastLockouts(const float *heatingLockoutC, const float *coolingLockoutC, uint8_t *buffer, size_t bufferSize)
  {
    const uint16_t bodyLength = 2 + (heatingLockoutC ? sizeof(float) : 0) + (coolingLockoutC ? sizeof(float) : 0);
    if (bufferSize < HmiEnvelope::HEADER_SIZE + bodyLength)
    {
      return 0;
    }

    HmiEnvelope::writeHeader(buffer, HmiMessageType::SetForecastLockouts, 0, 1, bodyLength);
    size_t offset = HmiEnvelope::HEADER_SIZE;
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
