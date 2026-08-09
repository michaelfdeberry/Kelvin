#include <Arduino.h>
#include "EnvironmentMonitor.h"
#include "Config.h"
#include "../battery/BatteryMonitor.h"
#include "../Common/SensorPayload.h"

#if ENV_SENSOR_TYPE == ENV_SENSOR_DHT11
#include "./sensors/DHT11Sensor.h"
#elif ENV_SENSOR_TYPE == ENV_SENSOR_SCD4X
#include "./sensors/SCD4XSensor.h"
#elif ENV_SENSOR_TYPE == ENV_SENSOR_SHT4X
#include "./sensors/SHT4XSensor.h"
#else
#error "Unsupported sensor type"
#endif

#if ENV_SENSOR_TYPE == ENV_SENSOR_DHT11
DHT11Sensor sensor;
#elif ENV_SENSOR_TYPE == ENV_SENSOR_SCD4X
SCD4XSensor sensor;
#elif ENV_SENSOR_TYPE == ENV_SENSOR_SHT4X
SHT4XSensor sensor;
#endif

BatteryMonitor batteryMonitor;

void EnvironmentMonitor::begin()
{
  sensor.begin();
  batteryMonitor.begin();
  lastUpdateSent = 0;
  memset(&lastPayload, 0, sizeof(sensor_payload));
}

bool EnvironmentMonitor::read(sensor_payload &payload)
{
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

  // Conditionally compile CO2 logic based on active hardware
#if ENV_SENSOR_TYPE == ENV_SENSOR_SCD4X
  bool hasCo2Change = abs(newPayload.co2 - lastPayload.co2) >= 75;
#else
  bool hasCo2Change = false;
#endif

  bool shouldUpdate = hasTempChange || hasHumChange || hasCo2Change || hasBatteryChange || isHeartbeatTime;

  if (shouldUpdate)
  {
    lastPayload = newPayload;
    lastUpdateSent = millis();
  }

  return shouldUpdate;
}
