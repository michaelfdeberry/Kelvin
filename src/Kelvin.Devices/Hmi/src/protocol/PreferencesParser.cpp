#include "PreferencesParser.h"

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
}

namespace PreferencesParser
{
  bool parse(const uint8_t *payload, size_t payloadLength, SystemPreferences &outPreferences)
  {
    size_t offset = 0;
    uint8_t temperatureUnit;
    if (!readByte(payload, payloadLength, offset, temperatureUnit))
    {
      return false;
    }
    outPreferences.temperatureUnit = static_cast<TemperatureUnit>(temperatureUnit);

    uint8_t timeFormat;
    if (!readByte(payload, payloadLength, offset, timeFormat))
    {
      return false;
    }
    outPreferences.timeFormat = static_cast<TimeFormat>(timeFormat);

    return true;
  }
}