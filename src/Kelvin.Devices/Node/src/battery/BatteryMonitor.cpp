#include <Arduino.h>
#include <PowerFeather.h>
#include "Config.h"
#include "Logger.h"
#include "./BatteryMonitor.h"

using namespace PowerFeather;

bool initialized = false;

void BatteryMonitor::begin()
{
  Result res = Board.setBatteryChargingMaxCurrent(BATTERY_CHARGING_CURRENT_MA);
  if (res != Result::Ok)
  {
    LOG_PRINTLN("Failed to set battery charging max current.");
  }

  res = Board.enableBatteryCharging(true);
  if (res != Result::Ok)
  {
    LOG_PRINTLN("Failed to enable battery charging.");
  }

  initialized = true;
}

battery_status BatteryMonitor::getStatus()
{
  battery_status status;

  if (!initialized)
  {
    LOG_PRINTLN("Board not initialized. Call begin() first.");
    return status;
  }

  uint8_t batteryCharge = 0;
  Result res = Board.getBatteryCharge(batteryCharge);

  if (res == Result::Ok)
  {
    LOG_PRINTF("Charge: %d %%\n", batteryCharge);
    status.level = batteryCharge;
  }
  else if (res == Result::InvalidState)
  {
    LOG_PRINTLN("Charge: <no battery configured>");
  }
  else
  {
    LOG_PRINTLN("Charge: <battery not detected>");
  }

  bool supplyGood = false;
  res = Board.checkSupplyGood(supplyGood);
  if (res == Result::Ok)
  {
    status.externalPower = supplyGood;
  }
  else
  {
    LOG_PRINTLN("Unable to determine whether external power is connected.");
  }

  return status;
}

void BatteryMonitor::enterShutdownMode()
{
  if (!initialized)
  {
    LOG_PRINTLN("Board not initialized. Call begin() first.");
    return;
  }

  bool supplyGood = false;
  Result res = Board.checkSupplyGood(supplyGood);
  if (res != Result::Ok)
  {
    LOG_PRINTLN("Unable to determine whether external power is connected.");
    return;
  }

  if (supplyGood)
  {
    LOG_PRINTLN("Shutdown rejected while external power is connected.");
    return;
  }

  LOG_PRINTLN("Entering shutdown mode.");
  res = Board.enterShutdownMode();
  if (res != Result::Ok)
  {
    LOG_PRINTLN("Failed to enter shutdown mode.");
  }
}
