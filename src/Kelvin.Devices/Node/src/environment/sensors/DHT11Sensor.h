#pragma once

#include "Config.h"
#include "../Common/SensorPayload.h"

class DHT11Sensor
{
public:
  void begin();
  bool read(sensor_payload &payload);
};