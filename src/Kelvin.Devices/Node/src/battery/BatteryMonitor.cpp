#include <Arduino.h>
#include "Config.h"
#include "./BatteryMonitor.h"

const int voltagePin = BATTERY_PIN;
const int multiplicationFactor = BATTERY_MULTIPLICATION_FACTOR;
const float deadVoltage = BATTERY_DEAD_VOLTAGE;
const float chargedVoltage = BATTERY_CHARGED_VOLTAGE;

void BatteryMonitor::begin()
{
  pinMode(voltagePin, INPUT);
  analogSetPinAttenuation(voltagePin, ADC_11db);
}

float BatteryMonitor::readVoltage()
{
  return (analogReadMilliVolts(voltagePin) / 1000.0f) * multiplicationFactor;
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

int BatteryMonitor::getBatteryLevel()
{
  float voltage = readAverageVoltage(10);
  float percentage = (voltage - deadVoltage) / (chargedVoltage - deadVoltage) * 100;

  if (percentage < 0)
    return 0;

  if (percentage > 100)
    return 100;

  return (int)percentage;
}
