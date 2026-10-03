#include <string.h>
#include "ControlStateParser.h"

namespace
{
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
}

namespace ControlStateParser
{
  bool parse(const uint8_t *payload, size_t payloadLength, ControlCallState &outState)
  {
    size_t offset = 0;
    uint8_t stateByte = 0;

    if (!readByte(payload, payloadLength, offset, stateByte))
      return false;
    outState.state = (ControlState)stateByte;

    if (!readOptionalFloat(payload, payloadLength, offset, outState.hasEnvironmentTemperature, outState.environmentTemperatureC))
      return false;
    if (!readOptionalFloat(payload, payloadLength, offset, outState.hasTargetTemperature, outState.targetTemperatureC))
      return false;
    if (!readOptionalFloat(payload, payloadLength, offset, outState.hasHumidity, outState.humidityPercentage))
      return false;
    if (!readOptionalFloat(payload, payloadLength, offset, outState.hasCO2, outState.co2LevelPpm))
      return false;

    return true;
  }
}
