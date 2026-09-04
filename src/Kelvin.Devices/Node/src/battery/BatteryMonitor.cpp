#include <Arduino.h>
#include <PowerFeather.h>
#include "Config.h"
#include "Logger.h"
#include "./BatteryMonitor.h"

using namespace PowerFeather;

bool initialized = false;

void BatteryMonitor::begin()
{
  // Board.init() is called once in Node.ino's setup(), before VSQT/STEMMA power is on
  Board.setBatteryChargingMaxCurrent(BATTERY_CHARGING_CURRENT_MA);
  Board.enableBatteryCharging(true);
  initialized = true;
}

int BatteryMonitor::getBatteryLevel()
{
  // uses the PowerFeather library to get battery voltage directly
  if (!initialized)
  {
    LOG_PRINTLN("Board not initialized. Call begin() first.");
    return -1; // Indicate an error
  }

  uint8_t batteryCharge = 0;
  Result res = Board.getBatteryCharge(batteryCharge);

  if (res == Result::Ok)
  {
    LOG_PRINTF("Charge: %d %%\n", batteryCharge);
    return batteryCharge;
  }
  else if (res == Result::InvalidState)
  {
    LOG_PRINTLN("Charge: <no battery configured>");
  }
  else
  {
    LOG_PRINTLN("Charge: <battery not detected>");
  }
  return -1; // Indicate an error
}
