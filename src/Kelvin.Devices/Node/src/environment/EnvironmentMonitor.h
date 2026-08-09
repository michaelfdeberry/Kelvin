#pragma once

#include "../Common/SensorPayload.h"

class EnvironmentMonitor
{
private:
  sensor_payload lastPayload{};
  unsigned long lastUpdateSent = 0;

public:
  void begin();
  bool read(sensor_payload &payload);
  bool shouldSendUpdate(const sensor_payload &newPayload);
};
