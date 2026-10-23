#include "HmiCbor.h"
#include "EnvironmentReadingParser.h"

namespace EnvironmentReadingParser
{
  bool parse(const uint8_t *payload, size_t payloadLength, EnvironmentAverageReading &outReading)
  {
    HmiCbor::Map map;
    EnvironmentAverageReading reading;
    if (!map.parse(payload, payloadLength) || !map.number(1, reading.temperatureC) ||
        !map.number(2, reading.humidityPercentage) || !map.number(3, reading.co2LevelPpm))
      return false;
    outReading = reading;
    return true;
  }
}
