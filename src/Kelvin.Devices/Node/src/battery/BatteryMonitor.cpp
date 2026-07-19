#include <Arduino.h>
#include "Config.h"
#include "./BatteryMonitor.h"

const int voltagePin = BATTERY_PIN;
const float referenceVoltage = BATTERY_REFERENCE_VOLTAGE;
const int adcResolution = BATTERY_ADC_RESOLUTION;
const int multiplicationFactor = BATTERY_MULTIPLICATION_FACTOR;

void BatteryMonitor::begin()
{
  pinMode(voltagePin, INPUT);
}

float BatteryMonitor::readVoltage()
{
  int rawValue = analogRead(voltagePin);
  float voltage = (rawValue / (float)adcResolution) * referenceVoltage;
  return voltage * multiplicationFactor;
}

float BatteryMonitor::readAverageVoltage(int samples)
{
  float sum = 0.0;
  for (int i = 0; i < samples; i++)
  {
    sum += readVoltage();
    delay(10);
  }
  return sum / samples;
}

int BatteryMonitor::getBatterLevel()
{
  // TODO: refactor this.
  // the voltages were moved to the config,
  // so this being hardcoded is not ideal.
  float voltage = readAverageVoltage(10);
  if (voltage >= 4.2)
    return 100;
  if (voltage >= 4.0)
    return 80;
  if (voltage >= 3.8)
    return 60;
  if (voltage >= 3.6)
    return 40;
  if (voltage >= 3.4)
    return 20;
  return 0;
}
