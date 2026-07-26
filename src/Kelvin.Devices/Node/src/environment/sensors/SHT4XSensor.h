
#pragma once

#include "../Common/SensorPayload.h"

class SHT4XSensor
{
public:
  void begin();
  bool read(sensor_payload &payload);
};