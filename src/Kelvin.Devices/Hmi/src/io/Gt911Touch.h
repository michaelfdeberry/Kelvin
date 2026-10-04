#pragma once

#include <Wire.h>

// Minimal GT911 capacitive touch reader over Arduino's Wire (ESP32_Display_Panel's touch path uses the legacy
// ESP-IDF I2C driver, which can't coexist with Wire on arduino-esp32 3.3.x).
class Gt911Touch
{
public:
  // Returns false if the controller doesn't ACK (wrong address, not out of reset, bus problem).
  bool begin(TwoWire &wire, uint8_t address = 0x5D);

  // Returns true while a finger is down; x/y are only written when it is.
  bool read(int16_t &x, int16_t &y);

private:
  static const uint16_t STATUS_REGISTER = 0x814E;
  static const uint16_t FIRST_POINT_REGISTER = 0x814F;

  TwoWire *bus = nullptr;
  uint8_t address = 0x5D;
  bool pressed = false;
  int16_t lastX = 0;
  int16_t lastY = 0;

  bool readRegister(uint16_t reg, uint8_t *buffer, size_t length);
  void writeRegister(uint16_t reg, uint8_t value);
};
