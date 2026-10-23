#include "PreferencesParser.h"
#include "HmiCbor.h"

namespace PreferencesParser
{
  bool parse(const uint8_t *payload, size_t payloadLength, SystemPreferences &outPreferences)
  {
    HmiCbor::Map map;
    uint32_t temperatureUnit, timeFormat;
    if (!map.parse(payload, payloadLength) || !map.unsignedValue(1, temperatureUnit, 1) || !map.unsignedValue(2, timeFormat, 1))
      return false;
    outPreferences.temperatureUnit = static_cast<TemperatureUnit>(temperatureUnit);
    outPreferences.timeFormat = static_cast<TimeFormat>(timeFormat);

    return true;
  }
}