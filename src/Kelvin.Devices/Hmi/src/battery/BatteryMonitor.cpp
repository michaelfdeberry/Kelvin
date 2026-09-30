#include <Arduino.h>
#include "Config.h"
#include "BatteryMonitor.h"

namespace
{
  const uint8_t ADC_REGISTER = 0x06;        // IO_EXTENSION_ADC_ADDR per esphome/esphome#10071
  const float ADC_REFERENCE_VOLTAGE = 9.9f; // empirical default from that same component
  const float ADC_MAX_VALUE = 1023.0f;      // 10-bit ADC

  // Not exposed by the CH32V003's register map - approximated from voltage since this panel is expected to
  // sit plugged in continuously, so a near-full-charge reading is a reasonable "on external power" signal.
  const float EXTERNAL_POWER_VOLTAGE_THRESHOLD = 4.15f;
}

void BatteryMonitor::begin(TwoWire &wire)
{
  bus = &wire;
}

battery_status BatteryMonitor::getStatus()
{
  battery_status status;

  bus->beginTransmission(IO_EXPANDER_I2C_ADDRESS);
  bus->write(ADC_REGISTER);
  if (bus->endTransmission() != 0)
  {
    return status;
  }

  if (bus->requestFrom(IO_EXPANDER_I2C_ADDRESS, 2) != 2)
  {
    return status;
  }

  uint8_t lowByte = bus->read();
  uint8_t highByte = bus->read();
  uint16_t adcValue = ((uint16_t)highByte << 8) | lowByte;

  float voltage = adcValue * ADC_REFERENCE_VOLTAGE / ADC_MAX_VALUE;
  float percentage = (voltage - BATTERY_EMPTY_VOLTAGE) / (BATTERY_FULL_VOLTAGE - BATTERY_EMPTY_VOLTAGE) * 100.0f;
  percentage = constrain(percentage, 0.0f, 100.0f);

  status.level = (int)percentage;
  status.externalPower = voltage >= EXTERNAL_POWER_VOLTAGE_THRESHOLD;
  return status;
}
