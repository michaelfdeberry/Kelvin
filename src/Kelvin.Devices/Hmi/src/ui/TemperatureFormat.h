#pragma once

#include <math.h>
#include <stdint.h>
#include <Preferences.h>

namespace TemperatureFormat
{
  inline bool isFahrenheit()
  {
    Preferences prefs;
    prefs.begin("system", true);
    bool tempF = prefs.getBool("tempF", false);
    prefs.end();
    return tempF;
  }

  inline const char *unitSymbol()
  {
    return isFahrenheit() ? "\u00B0F" : "\u00B0C";
  }

  inline float presentAsPreferredUnit(float celsius)
  {
    return isFahrenheit() ? celsius * 9.0f / 5.0f + 32.0f : celsius;
  }

  // Whole degrees in the preferred unit, for the integer-valued slider.
  inline int32_t convertToPreferredUnit(float celsius)
  {
    return (int32_t)lroundf(presentAsPreferredUnit(celsius));
  }

  inline float fromPreferredUnit(int32_t value)
  {
    return isFahrenheit() ? ((float)value - 32.0f) * 5.0f / 9.0f : (float)value;
  }

  // Converts a Fahrenheit design bound into the preferred unit (whole degrees).
  inline int32_t preferredFromFahrenheit(int32_t fahrenheit)
  {
    return isFahrenheit() ? fahrenheit : (int32_t)lroundf(((float)fahrenheit - 32.0f) * 5.0f / 9.0f);
  }
}
