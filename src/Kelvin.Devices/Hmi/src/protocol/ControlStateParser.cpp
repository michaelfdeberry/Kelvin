#include "HmiCbor.h"
#include "ControlStateParser.h"

namespace ControlStateParser
{
  bool parse(const uint8_t *payload, size_t payloadLength, ControlCallState &outState)
  {
    HmiCbor::Map map;
    ControlCallState state;
    uint32_t stateValue;
    if (!map.parse(payload, payloadLength) || !map.unsignedValue(1, stateValue, 8) ||
        !map.nullableNumber(2, state.hasEnvironmentTemperature, state.environmentTemperatureC) ||
        !map.nullableNumber(3, state.hasTargetTemperature, state.targetTemperatureC) ||
        !map.nullableNumber(4, state.hasHumidity, state.humidityPercentage) ||
        !map.nullableNumber(5, state.hasCO2, state.co2LevelPpm))
      return false;
    state.state = (ControlState)stateValue;
    outState = state;
    return true;
  }
}
