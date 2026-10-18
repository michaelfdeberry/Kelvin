#include <Arduino.h>
#include <Logger.h>
#include "Config.h"
#include "BatteryMonitor.h"

static_assert(BATTERY_PACK_APA >= 0 && BATTERY_PACK_APA <= 0xFF, "Battery APA must fit in one byte");
static_assert(BATTERY_TEMPERATURE_C >= -20.0f && BATTERY_TEMPERATURE_C <= 60.0f, "Battery temperature must be between -20 and 60 C");

void BatteryMonitor::begin(TwoWire &wire)
{
  bus = &wire;
  initialized = gauge.begin(bus) &&
                gauge.setPackAPA(BATTERY_PACK_APA) &&
                gauge.setTemperatureMode(LC709203F_TEMPERATURE_I2C) &&
                gauge.writeWord(LC709203F_CMD_CELLTEMPERATURE, (uint16_t)((BATTERY_TEMPERATURE_C + 273.15f) * 10.0f + 0.5f));

  if (!initialized)
  {
    LOG_PRINTLN("LC709203F initialization failed; battery level unavailable.");
  }
}

battery_status BatteryMonitor::getStatus()
{
  battery_status status;
  if (!initialized)
  {
    begin(*bus);
    if (!initialized)
    {
      return status;
    }
  }

  uint16_t voltageMillivolts = 0;
  uint16_t percentageTenths = 0;
  if (!gauge.readWord(LC709203F_CMD_CELLVOLTAGE, &voltageMillivolts) ||
      !gauge.readWord(LC709203F_CMD_CELLITE, &percentageTenths) ||
      voltageMillivolts < 2500 || voltageMillivolts > 4500 || percentageTenths > 1000)
  {
    LOG_PRINTLN("LC709203F read failed or invalid; battery level unavailable.");
    initialized = false;
    return status;
  }

  if (hasPreviousReading)
  {
    if (percentageTenths > previousPercentageTenths)
    {
      charging = true;
    }
    else if (percentageTenths < previousPercentageTenths)
    {
      charging = false;
    }
  }

  previousPercentageTenths = percentageTenths;
  hasPreviousReading = true;
  status.level = (percentageTenths + 5) / 10;
  status.externalPower = charging;
  return status;
}