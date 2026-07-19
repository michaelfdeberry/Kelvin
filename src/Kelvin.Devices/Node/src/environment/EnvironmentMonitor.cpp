#include "EnvironmentMonitor.h"
#include "Config.h"
#include "../battery/BatteryMonitor.h"
#include "../Common/SensorPayload.h"

#if ENV_SENSOR_TYPE == ENV_SENSOR_DHT11
#include "./sensors/DHT11Sensor.h"
#elif ENV_SENSOR_TYPE == ENV_SENSOR_SCD4X
#include "./sensors/SCD4XSensor.h"
#else
#error "Unsupported sensor type"
#endif

#if ENV_SENSOR_TYPE == ENV_SENSOR_DHT11
DHT11Sensor sensor;
#elif ENV_SENSOR_TYPE == ENV_SENSOR_SCD4X
SCD4XSensor sensor;
#endif

BatteryMonitor batteryMonitor;

void EnvironmentMonitor::begin()
{
  sensor.begin();
  batteryMonitor.begin();
}

bool EnvironmentMonitor::read(sensor_payload &payload)
{
  bool result = sensor.read(payload);
  if (result)
  {
    payload.batteryLevel = batteryMonitor.getBatterLevel();
  }
  return result;
}