#pragma once

struct battery_status
{
  int level = -1; // percent, -1 when the charge could not be read
  bool externalPower = false;
};

class BatteryMonitor
{
public:
  void begin();
  battery_status getStatus();
  void enterShutdownMode();
};
