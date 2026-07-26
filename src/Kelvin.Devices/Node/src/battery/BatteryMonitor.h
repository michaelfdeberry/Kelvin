#pragma once

class BatteryMonitor
{
public:
  void begin();
  float readVoltage();
  float readAverageVoltage(int samples);
  int getBatteryLevel();
};
