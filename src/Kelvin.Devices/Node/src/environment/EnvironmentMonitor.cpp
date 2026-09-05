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
SensirionI2cSht4x sht4x;

// Ticks are counted in units of the timer-wake interval; survives deep sleep in RTC memory.
#define HEARTBEAT_TICKS (HEARTBEAT_INTERVAL_S / TIMER_WAKE_INTERVAL_S)

static_assert(HEARTBEAT_TICKS >= 1, "HEARTBEAT_INTERVAL_S must be at least one wake interval");
static_assert(HEARTBEAT_TICKS * TIMER_WAKE_INTERVAL_S == HEARTBEAT_INTERVAL_S, "HEARTBEAT_INTERVAL_S must be a whole multiple of TIMER_WAKE_INTERVAL_S");

RTC_DATA_ATTR static sensor_payload lastPayload{};
RTC_DATA_ATTR static uint32_t heartbeatTicks = HEARTBEAT_TICKS; // force a send on the first reading after power-up

void EnvironmentMonitor::begin()
{
  sht4x.begin(Wire1, SHT40_I2C_ADDR_44);
  sht4x.softReset();

  batteryMonitor.begin();
}

bool EnvironmentMonitor::read(sensor_payload &payload)
{
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

void EnvironmentMonitor::enterShutdownMode()
{
  batteryMonitor.enterShutdownMode();
}
