#pragma once

#include <Wire.h>

struct battery_status
{
  int level = -1; // percent, -1 when the reading could not be obtained
  bool externalPower = false;
};

// Reads battery voltage through the CH32V003 helper MCU this board uses as its I/O expander (I2C address
// 0x24, see IO_EXPANDER_I2C_ADDRESS in Config.h) - the ESP32-S3's own pins are almost entirely consumed by
// the display, so battery monitoring is routed through this chip's ADC register instead of a direct analog
// pin. The register map and the empirical ADC reference voltage are taken from ESPHome's merged
// waveshare_io_ch32v003 component (esphome/esphome#10071), not guessed. If a fuller IO-expander driver
// (backlight PWM, touch reset) is ever added for this chip, this should share that I2C conduit instead of
// opening its own.
class BatteryMonitor
{
public:
  void begin(TwoWire &wire = Wire);
  battery_status getStatus();

private:
  TwoWire *bus = &Wire;
};
