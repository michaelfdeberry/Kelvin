#include "HmiCbor.h"
#include "HmiCommandEncoder.h"

namespace HmiCommandEncoder
{
  size_t encodeSensorReading(float temperatureC, float humidityPercentage, float batteryLevelPercentage, uint8_t *buffer, size_t bufferSize)
  {
    HmiCbor::Writer writer(buffer, bufferSize, HmiMessageType::SensorReading, 4);
    writer.number(1, temperatureC);
    writer.number(2, humidityPercentage);
    writer.unsignedValue(3, 0);
    writer.number(4, batteryLevelPercentage);
    return writer.finish();
  }

  size_t encodeSetMode(RunMode mode, uint8_t *buffer, size_t bufferSize)
  {
    if ((uint8_t)mode > 4)
      return 0;
    HmiCbor::Writer writer(buffer, bufferSize, HmiMessageType::SetMode, 1);
    writer.unsignedValue(1, (uint8_t)mode);
    return writer.finish();
  }

  size_t encodeSetFanEnabled(bool enabled, uint8_t *buffer, size_t bufferSize)
  {
    HmiCbor::Writer writer(buffer, bufferSize, HmiMessageType::SetFanEnabled, 1);
    writer.boolean(1, enabled);
    return writer.finish();
  }

  size_t encodeSetSetPoint(RunType type, float targetTemperatureC, uint8_t *buffer, size_t bufferSize)
  {
    if ((uint8_t)type > 1)
      return 0;
    HmiCbor::Writer writer(buffer, bufferSize, HmiMessageType::SetSetPoint, 2);
    writer.unsignedValue(1, (uint8_t)type);
    writer.number(2, targetTemperatureC);
    return writer.finish();
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
    if ((uint8_t)type > 1 || startMinutes > 1439 || endMinutes > 1439)
      return 0;
    HmiCbor::Writer writer(buffer, bufferSize, HmiMessageType::UpsertSchedule, 5);
    writer.id(1, scheduleId);
    writer.unsignedValue(2, (uint8_t)type);
    writer.unsignedValue(3, startMinutes);
    writer.unsignedValue(4, endMinutes);
    writer.number(5, targetTemperatureC);
    return writer.finish();
  }

  size_t encodeRemoveSchedule(const uint8_t scheduleId[16], uint8_t *buffer, size_t bufferSize)
  {
    if (!scheduleId)
      return 0;
    HmiCbor::Writer writer(buffer, bufferSize, HmiMessageType::RemoveSchedule, 1);
    writer.id(1, scheduleId);
    return writer.finish();
  }

  size_t encodeSetForecastLockouts(const float *heatingLockoutC, const float *coolingLockoutC, uint8_t *buffer, size_t bufferSize)
  {
    HmiCbor::Writer writer(buffer, bufferSize, HmiMessageType::SetForecastLockouts, 2);
    writer.nullableNumber(1, heatingLockoutC);
    writer.nullableNumber(2, coolingLockoutC);
    return writer.finish();
  }

  size_t encodeRequestState(uint8_t *buffer, size_t bufferSize)
  {
    HmiCbor::Writer writer(buffer, bufferSize, HmiMessageType::RequestState, 0);
    return writer.finish();
  }
}
