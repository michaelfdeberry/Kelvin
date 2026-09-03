#include <Wire.h>
#include <Arduino.h>
#include <PowerFeather.h>
#include <SensirionI2cSht4x.h>
#include "Config.h"
#include "EnvironmentMonitor.h"
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

void EnvironmentMonitor::begin()
{
  sht4x.begin(StemmaWire, SHT40_I2C_ADDR_44);
  sht4x.softReset();

  batteryMonitor.begin();
  lastUpdateSent = 0;

  memset(&lastPayload, 0, sizeof(sensor_payload));
}

bool EnvironmentMonitor::read(sensor_payload &payload)
{
  delay(100);

  float temperature = 0.0;
  float relativeHumidity = 0.0;

  error = sht4x.measureHighPrecision(temperature, relativeHumidity);
  if (error != NO_ERROR)
  {
#if defined(DEBUG)
    Serial.print("Error trying to execute measureHighPrecision(): ");
    errorToString(error, errorMessage, sizeof errorMessage);
    Serial.println(errorMessage);
#endif
    return false;
  }

  payload.temperature = temperature;
  payload.humidity = relativeHumidity;
  payload.co2 = 0;

  bool result = sensor.read(payload);
  if (result)
  {
    payload.batteryLevel = batteryMonitor.getBatteryLevel();
  }

  return result;
}

bool EnvironmentMonitor::shouldSendUpdate(const sensor_payload &newPayload)
{
  bool hasTempChange = fabs(newPayload.temperature - lastPayload.temperature) >= 0.5;
  bool hasHumChange = fabs(newPayload.humidity - lastPayload.humidity) >= 1.0;
  bool hasBatteryChange = abs(newPayload.batteryLevel - lastPayload.batteryLevel) >= 5;
  bool isHeartbeatTime = (millis() - lastUpdateSent) >= (60 * 1000 * 5);
  bool shouldUpdate = hasTempChange || hasHumChange || hasBatteryChange || isHeartbeatTime;

  if (shouldUpdate)
  {
    lastPayload = newPayload;
    lastUpdateSent = millis();
  }

  return shouldUpdate;
}
