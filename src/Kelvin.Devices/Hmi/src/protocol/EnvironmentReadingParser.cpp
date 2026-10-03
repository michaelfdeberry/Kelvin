#include <string.h>
#include "EnvironmentReadingParser.h"

namespace
{
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
}

namespace EnvironmentReadingParser
{
  bool parse(const uint8_t *payload, size_t payloadLength, EnvironmentAverageReading &outReading)
  {
    size_t offset = 0;

    if (!readFloat(payload, payloadLength, offset, outReading.temperatureC))
      return false;
    if (!readFloat(payload, payloadLength, offset, outReading.humidityPercentage))
      return false;
    if (!readFloat(payload, payloadLength, offset, outReading.co2LevelPpm))
      return false;

    return true;
  }
}
