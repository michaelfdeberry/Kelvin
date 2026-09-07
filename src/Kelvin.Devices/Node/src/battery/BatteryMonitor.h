#pragma once

class BatteryMonitor
{
public:
  void begin();
  int getBatteryLevel();
  void enterShutdownMode();
};
