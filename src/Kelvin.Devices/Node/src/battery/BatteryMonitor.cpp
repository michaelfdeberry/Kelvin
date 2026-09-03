#include <Arduino.h>
#include <PowerFeather.h>
#include "Config.h"
#include "./BatteryMonitor.h"

using namespace PowerFeather;

const int voltagePin = BATTERY_PIN;
const int multiplicationFactor = BATTERY_MULTIPLICATION_FACTOR;
const float deadVoltage = BATTERY_DEAD_VOLTAGE;
const float chargedVoltage = BATTERY_CHARGED_VOLTAGE;
bool initialized = false;

void BatteryMonitor::begin()
{
  Result initResult = Board.init(BATTERY_CAPACITY_MAH);
  if (initResult == Result::Ok)
  {
#if defined(DEBUG)
    Serial.println("Board initialized successfully\n\n");
#endif
    Board.setBatteryChargingMaxCurrent(BATTERY_CHARGING_CURRENT_MA);
    Board.enableBatteryCharging(true);
    initialized = true;
  }
}

int BatteryMonitor::getBatteryLevel()
{
  // uses the PowerFeather library to get battery voltage directly
  if (!initialized)
  {
#if defined(DEBUG)
    Serial.println("Board not initialized. Call begin() first.\n");
#endif
    return -1; // Indicate an error
  }

  uint8_t batteryCharge = 0;
  Result res = Board.getBatteryCharge(batteryCharge);

  if (res == Result::Ok)
  {
#if defined(DEBUG)
    Serial.println("Charge: %d %%\n", batteryCharge);
#endif
    return batteryCharge;
  }
  else if (res == Result::InvalidState)
  {
#if defined(DEBUG)
    Serial.println("Charge: <no battery configured>\n");
#endif
  }
  else
  {
#if defined(DEBUG)
    Serial.println("Charge: <battery not detected>\n");
#endif
  }
  return -1; // Indicate an error
}
