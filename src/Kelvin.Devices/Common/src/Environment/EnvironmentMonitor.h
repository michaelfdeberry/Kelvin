#pragma once

#include <Wire.h>
#include "../SensorPayload.h"

class EnvironmentMonitor
{
public:
  // Defaults to the primary I2C bus; pass Wire1 (or another configured TwoWire) for boards - like the
  // PowerFeather - that wire the sensor to a secondary bus instead.
  void begin(TwoWire &wire = Wire);
  bool read(sensor_payload &payload);
  bool shouldSendUpdate(const sensor_payload &newPayload);

private:
  TwoWire *bus = &Wire;
};
