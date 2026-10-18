#include "HmiCbor.h"
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

namespace ThermostatStateParser
{
  bool parse(const uint8_t *data, size_t length, ThermostatState &outState)
  {
    HmiCbor::Map map;
    ThermostatState state;
    uint32_t mode;
    if (!map.parse(data, length) || !map.unsignedValue(1, mode, 4) || !map.boolean(2, state.fanEnabled) ||
        !map.number(3, state.hysteresisC) || !map.nullableNumber(4, state.hasHeatingLockout, state.heatingLockoutC) ||
        !map.nullableNumber(5, state.hasCoolingLockout, state.coolingLockoutC))
      return false;
    state.mode = (RunMode)mode;
    CborValue entries;
    if (!map.array(6, entries, state.setPointCount, MAX_SET_POINTS))
      return false;
    for (size_t index = 0; index < state.setPointCount; index++)
    {
      HmiCbor::Map entry;
      uint32_t type;
      if (!entry.read(entries) || !entry.unsignedValue(1, type, 1) || !entry.number(2, state.setPoints[index].targetTemperatureC))
        return false;
      state.setPoints[index].type = (RunType)type;
    }
    if (!map.array(7, entries, state.scheduleCount, MAX_SCHEDULES))
      return false;
    for (size_t index = 0; index < state.scheduleCount; index++)
    {
      HmiCbor::Map entry;
      auto &schedule = state.schedules[index];
      uint32_t type, start, end;
      if (!entry.read(entries) || !entry.id(1, schedule.id) || !entry.unsignedValue(2, type, 1) ||
          !entry.unsignedValue(3, start, 1439) || !entry.unsignedValue(4, end, 1439) || !entry.number(5, schedule.targetTemperatureC))
        return false;
      schedule.type = (RunType)type;
      schedule.startMinutes = (uint16_t)start;
      schedule.endMinutes = (uint16_t)end;
    }
    outState = state;
    return true;
  }
}
