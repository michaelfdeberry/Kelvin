#include <Arduino.h>
#include "Config.h"
#include "BatteryMonitor.h"

namespace
{
  const uint8_t ADC_REGISTER = 0x06;        // IO_EXTENSION_ADC_ADDR per esphome/esphome#10071
  const float ADC_REFERENCE_VOLTAGE = 9.9f; // empirical default from that same component
  const float ADC_MAX_VALUE = 1023.0f;      // 10-bit ADC
  const float EXTERNAL_POWER_VOLTAGE_THRESHOLD = 4.15f;

  struct VoltagePoint
  {
    float voltage;
    int percentage;
  };

  // Standard 3.7V LiPo discharge curve lookup table
  const VoltagePoint LIPO_DISCHARGE_CURVE[] = {
      {4.20f, 100},
      {4.10f, 95},
      {4.00f, 85},
      {3.90f, 75},
      {3.80f, 50},
      {3.70f, 25},
      {3.60f, 10},
      {3.50f, 5},
      {3.20f, 0}};
  const size_t CURVE_POINT_COUNT = sizeof(LIPO_DISCHARGE_CURVE) / sizeof(LIPO_DISCHARGE_CURVE[0]);

  int interpolatePercentage(float voltage)
  {
    if (voltage >= LIPO_DISCHARGE_CURVE[0].voltage)
      return 100;
    if (voltage <= LIPO_DISCHARGE_CURVE[CURVE_POINT_COUNT - 1].voltage)
      return 0;

    for (size_t i = 0; i < CURVE_POINT_COUNT - 1; i++)
    {
      if (voltage <= LIPO_DISCHARGE_CURVE[i].voltage &&
          voltage >= LIPO_DISCHARGE_CURVE[i + 1].voltage)
      {

        float vUpper = LIPO_DISCHARGE_CURVE[i].voltage;
        float vLower = LIPO_DISCHARGE_CURVE[i + 1].voltage;
        int pUpper = LIPO_DISCHARGE_CURVE[i].percentage;
        int pLower = LIPO_DISCHARGE_CURVE[i + 1].percentage;

        return pLower + (int)((voltage - vLower) * (pUpper - pLower) / (vUpper - vLower));
      }
    }
    return 0;
  }
}

void BatteryMonitor::begin(TwoWire &wire)
{
  bus = &wire;
  _isInitialized = false;
  _emaVoltage = 0.0f;
}

float BatteryMonitor::readRawVoltage()
{
  if (!bus)
    return 0.0f;

  uint32_t totalAdc = 0;
  int validSamples = 0;
  const int burstSamples = 5; // Rapid I2C burst reads to damp out electrical noise

  for (int i = 0; i < burstSamples; i++)
  {
    bus->beginTransmission(IO_EXPANDER_I2C_ADDRESS);
    bus->write(ADC_REGISTER);
    if (bus->endTransmission() == 0)
    {
      if (bus->requestFrom((uint8_t)IO_EXPANDER_I2C_ADDRESS, (uint8_t)2) == 2)
      {
        uint8_t lowByte = bus->read();
        uint8_t highByte = bus->read();
        uint16_t adcValue = ((uint16_t)highByte << 8) | lowByte;

        totalAdc += adcValue;
        validSamples++;
      }
    }
    delay(2); // Short delay to let the I2C bus and converter settle
  }

  if (validSamples == 0)
  {
    return 0.0f;
  }

  float avgAdc = (float)totalAdc / validSamples;
  return avgAdc * ADC_REFERENCE_VOLTAGE / ADC_MAX_VALUE;
}

battery_status BatteryMonitor::getStatus()
{
  battery_status status;
  status.level = 0;
  status.externalPower = false;

  float rawVoltage = readRawVoltage();
  if (rawVoltage <= 0.0f)
  {
    return status;
  }

  // Apply Exponential Moving Average (EMA) filtering (Alpha = 0.1)
  const float alpha = 0.1f;
  if (!_isInitialized)
  {
    _emaVoltage = rawVoltage;
    _isInitialized = true;
  }
  else
  {
    _emaVoltage = (alpha * rawVoltage) + ((1.0f - alpha) * _emaVoltage);
  }

  status.level = interpolatePercentage(_emaVoltage);
  status.externalPower = _emaVoltage >= EXTERNAL_POWER_VOLTAGE_THRESHOLD;

  return status;
}