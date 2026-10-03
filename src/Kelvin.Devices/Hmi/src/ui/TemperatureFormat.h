#pragma once

#include <math.h>
#include <stdint.h>

// The panel has no access to a user's preferred units (no preferences sync wire message exists yet), so
// display is hardcoded to Fahrenheit (matching the screenshots this UI was built from) while every value
// sent to/from the server stays in Celsius, same boundary pattern as Kelvin.Client's
// convertToPreferredUnit/fromPreferredUnit.
namespace TemperatureFormat
{
  inline int32_t celsiusToWholeFahrenheit(float celsius)
  {
    return (int32_t)lroundf(celsius * 9.0f / 5.0f + 32.0f);
  }

  inline float wholeFahrenheitToCelsius(int32_t fahrenheit)
  {
    return ((float)fahrenheit - 32.0f) * 5.0f / 9.0f;
  }
}
