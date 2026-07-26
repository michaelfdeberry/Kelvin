#include <Arduino.h>
#include "Config.h"
#include "./BatteryMonitor.h"

const int voltagePin = BATTERY_PIN;
const float referenceVoltage = BATTERY_REFERENCE_VOLTAGE;
const int adcResolution = BATTERY_ADC_RESOLUTION;
const int multiplicationFactor = BATTERY_MULTIPLICATION_FACTOR;
const float deadVoltage = BATTERY_DEAD_VOLTAGE;
const float chargedVoltage = BATTERY_CHARGED_VOLTAGE;

void BatteryMonitor::begin()
{
  pinMode(voltagePin, INPUT);
}

float BatteryMonitor::readVoltage()
{
  int rawValue = analogRead(voltagePin);
  return rawValue * (referenceVoltage / (float)adcResolution) * multiplicationFactor;
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
  return (voltage - deadVoltage) / (chargedVoltage - deadVoltage) * 100;
}
