#pragma once

#include <Wire.h>

struct battery_status
{
  int level = -1; // percent, -1 when the reading could not be obtained
  bool externalPower = false;
};

// Reads battery voltage through the CH32V003-like helper MCU this board uses as its I/O expander (I2C
// address 0x24, see IO_EXPANDER_I2C_ADDRESS in Config.h) - the ESP32-S3's own pins are almost entirely
// consumed by the display, so battery monitoring is routed through this chip's ADC register instead of a
// direct analog pin. The register map and the empirical ADC reference voltage are taken from ESPHome's
// merged waveshare_io_ch32v003 component (esphome/esphome#10071), independently confirmed via Waveshare's
// official ESP32-S3-Touch-LCD-7B Arduino demo source (io_extension.h/.cpp). `../io/IoExtension` implements
// this same chip's Mode/Output registers (backlight, touch reset) - it doesn't share state with this
// class's ADC-only reads since they touch different registers.
class BatteryMonitor
{
public:
  void begin(TwoWire &wire = Wire);
  battery_status getStatus();

private:
  TwoWire *bus = &Wire;
};
