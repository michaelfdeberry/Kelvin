#include <string.h>
#include "ThermostatStateParser.h"

const SetPointState *ThermostatState::findSetPoint(RunType type) const
{
  for (size_t i = 0; i < setPointCount; i++)
  {
    if (setPoints[i].type == type)
    {
      return &setPoints[i];
    }
  }
  return nullptr;
}

namespace
{
  // Advances `offset` past the field and returns false (without modifying `offset` further) if the buffer
  // doesn't have enough bytes remaining.
  bool readByte(const uint8_t *data, size_t length, size_t &offset, uint8_t &outValue)
  {
    if (offset + sizeof(uint8_t) > length)
    {
      return false;
    }
    outValue = data[offset];
    offset += sizeof(uint8_t);
    return true;
  }

  bool readUInt16(const uint8_t *data, size_t length, size_t &offset, uint16_t &outValue)
  {
    if (offset + sizeof(uint16_t) > length)
    {
      return false;
    }
    memcpy(&outValue, data + offset, sizeof(uint16_t));
    offset += sizeof(uint16_t);
    return true;
  }

  bool readFloat(const uint8_t *data, size_t length, size_t &offset, float &outValue)
  {
    if (offset + sizeof(float) > length)
    {
      return false;
    }
    memcpy(&outValue, data + offset, sizeof(float));
    offset += sizeof(float);
    return true;
  }

  bool readOptionalFloat(const uint8_t *data, size_t length, size_t &offset, bool &hasValue, float &outValue)
  {
    uint8_t hasValueByte = 0;
    if (!readByte(data, length, offset, hasValueByte))
    {
      return false;
    }
    hasValue = hasValueByte != 0;
    if (!hasValue)
    {
      return true;
    }
    return readFloat(data, length, offset, outValue);
  }

  bool readBytes(const uint8_t *data, size_t length, size_t &offset, uint8_t *outBuffer, size_t count)
  {
    if (offset + count > length)
    {
      return false;
    }
    memcpy(outBuffer, data + offset, count);
    offset += count;
    return true;
  }
}

namespace ThermostatStateParser
{
  bool parse(const uint8_t *data, size_t length, ThermostatState &outState)
  {
    size_t offset = 0;
    uint8_t modeByte = 0;
    uint8_t fanEnabledByte = 0;

    if (!readByte(data, length, offset, modeByte))
      return false;
    if (!readByte(data, length, offset, fanEnabledByte))
      return false;
    if (!readFloat(data, length, offset, outState.hysteresisC))
      return false;
    if (!readOptionalFloat(data, length, offset, outState.hasHeatingLockout, outState.heatingLockoutC))
      return false;
    if (!readOptionalFloat(data, length, offset, outState.hasCoolingLockout, outState.coolingLockoutC))
      return false;

    outState.mode = (RunMode)modeByte;
    outState.fanEnabled = fanEnabledByte != 0;

    uint8_t setPointCount = 0;
    if (!readByte(data, length, offset, setPointCount))
      return false;

    outState.setPointCount = 0;
    for (uint8_t i = 0; i < setPointCount; i++)
    {
      uint8_t typeByte = 0;
      float targetTemperatureC = 0.0f;
      if (!readByte(data, length, offset, typeByte))
        return false;
      if (!readFloat(data, length, offset, targetTemperatureC))
        return false;

      if (outState.setPointCount < MAX_SET_POINTS)
      {
        outState.setPoints[outState.setPointCount].type = (RunType)typeByte;
        outState.setPoints[outState.setPointCount].targetTemperatureC = targetTemperatureC;
        outState.setPointCount++;
      }
    }

    uint8_t scheduleCount = 0;
    if (!readByte(data, length, offset, scheduleCount))
      return false;

    outState.scheduleCount = 0;
    for (uint8_t i = 0; i < scheduleCount; i++)
    {
      uint8_t id[16];
      uint8_t typeByte = 0;
      uint16_t startMinutes = 0;
      uint16_t endMinutes = 0;
      float targetTemperatureC = 0.0f;

      if (!readBytes(data, length, offset, id, sizeof(id)))
        return false;
      if (!readByte(data, length, offset, typeByte))
        return false;
      if (!readUInt16(data, length, offset, startMinutes))
        return false;
      if (!readUInt16(data, length, offset, endMinutes))
        return false;
      if (!readFloat(data, length, offset, targetTemperatureC))
        return false;

      if (outState.scheduleCount < MAX_SCHEDULES)
      {
        ScheduleState &schedule = outState.schedules[outState.scheduleCount];
        memcpy(schedule.id, id, sizeof(id));
        schedule.type = (RunType)typeByte;
        schedule.startMinutes = startMinutes;
        schedule.endMinutes = endMinutes;
        schedule.targetTemperatureC = targetTemperatureC;
        outState.scheduleCount++;
      }
    }

    return true;
  }
}
