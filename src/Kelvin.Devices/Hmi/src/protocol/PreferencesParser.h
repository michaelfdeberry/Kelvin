#pragma once

#include <stddef.h>
#include <stdint.h>
#include <HmiProtocol.h>

struct SystemPreferences
{
  TemperatureUnit temperatureUnit;
  TimeFormat timeFormat;
};

namespace PreferencesParser
{
  bool parse(const uint8_t *payload, size_t payloadLength, SystemPreferences &outPreferences);
}