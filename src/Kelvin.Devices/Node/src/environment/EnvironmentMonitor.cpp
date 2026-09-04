#include <Wire.h>
#include <Arduino.h>
#include <PowerFeather.h>
#include <SensirionI2cSht4x.h>
#include "Config.h"
#include "EnvironmentMonitor.h"
#include "Logger.h"
#include "../Common/SensorPayload.h"
#include "../battery/BatteryMonitor.h"

#ifdef NO_ERROR
#undef NO_ERROR
#endif
#define NO_ERROR 0

static char errorMessage[64];
static int16_t error;

BatteryMonitor batteryMonitor;
TwoWire StemmaWire = TwoWire(1);
SensirionI2cSht4x sht4x;

// Ticks are counted in units of the timer-wake interval; survives deep sleep in RTC memory.
#define HEARTBEAT_TICKS 10

RTC_DATA_ATTR static sensor_payload lastPayload{};
RTC_DATA_ATTR static uint32_t heartbeatTicks = HEARTBEAT_TICKS; // force a send on the first reading after power-up

void EnvironmentMonitor::begin()
{
  sht4x.begin(StemmaWire, SHT40_I2C_ADDR_44);
  sht4x.softReset();

  batteryMonitor.begin();
}

bool EnvironmentMonitor::read(sensor_payload &payload)
{
  delay(100);

  float temperature = 0.0;
  float relativeHumidity = 0.0;

  error = sht4x.measureHighPrecision(temperature, relativeHumidity);
  if (error != NO_ERROR)
  {
    errorToString(error, errorMessage, sizeof errorMessage);
    LOG_PRINT("Error trying to execute measureHighPrecision(): ");
    LOG_PRINTLN(errorMessage);
    return false;
  }

  payload.temperature = temperature;
  payload.humidity = relativeHumidity;
  payload.co2 = 0;
  payload.batteryLevel = batteryMonitor.getBatteryLevel();

  return true;
}

bool EnvironmentMonitor::shouldSendUpdate(const sensor_payload &newPayload)
{
  bool hasTempChange = fabs(newPayload.temperature - lastPayload.temperature) >= 0.5;
  bool hasHumChange = fabs(newPayload.humidity - lastPayload.humidity) >= 1.0;
  bool hasBatteryChange = abs(newPayload.batteryLevel - lastPayload.batteryLevel) >= 5;
  bool isHeartbeatTime = heartbeatTicks >= HEARTBEAT_TICKS;
  bool shouldUpdate = hasTempChange || hasHumChange || hasBatteryChange || isHeartbeatTime;

  if (shouldUpdate)
  {
    lastPayload = newPayload;
    heartbeatTicks = 0;
  }
  else
  {
    heartbeatTicks++;
  }

  return shouldUpdate;
}
