#pragma once

#include "../Common/SensorPayload.h"

class SCD4XSensor
{
public:
  void begin();
  bool read(sensor_payload &payload);
};