#pragma once

#include "../Common/SensorPayload.h"

class EnvironmentMonitor
{
public:
  void begin();
  bool read(sensor_payload &payload);
};
