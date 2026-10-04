#pragma once

#include <Wire.h>

// Drives the same "IO EXTENSION" helper chip BatteryMonitor reads its ADC register from (I2C address
// 0x24, see IO_EXPANDER_I2C_ADDRESS in Config.h) - confirmed via Waveshare's official
// ESP32-S3-Touch-LCD-7B Arduino demo source (examples/06_LCD/io_extension.h/.cpp) to be a single-address,
// register-mapped chip (not CH422G, which only applies to the plain non-B "7" board).
//
// Only the Mode and Output registers are implemented here (what backlight/touch-reset need) - Input/PWM/
// ADC are unused by this driver; BatteryMonitor.cpp reads the ADC register directly since it doesn't need
// to share any output-byte state with this class.
namespace IoExtensionPin
{
  // Pin assignments per the official demo's io_extension.h comments.
  const uint8_t TOUCH_RESET = 1;
  const uint8_t BACKLIGHT = 2;
}

class IoExtension
{
public:
  void begin(TwoWire &wire = Wire);

  // Sets all pins to output mode - must be called once before setOutput().
  void initialize();

  // Sets a single output pin high/low, preserving the other pins' last-written state.
  void setOutput(uint8_t pin, bool value);

private:
  static const uint8_t MODE_REGISTER = 0x02;
  static const uint8_t OUTPUT_REGISTER = 0x03;

  TwoWire *bus = &Wire;
  uint8_t outputValue = 0xFF;

  void writeRegister(uint8_t reg, uint8_t value);
};
