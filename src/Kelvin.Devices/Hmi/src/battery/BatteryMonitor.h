#pragma once

#include <Wire.h>
#include <Adafruit_LC709203F.h>

struct battery_status
{
  int level = -1; // percent, -1 when the reading could not be obtained
  bool externalPower = false;
};

// Reads the external LC709203F at 0x0B on the panel's shared I2C bus.
class BatteryMonitor
{
public:
  void begin(TwoWire &wire = Wire);
  battery_status getStatus();

private:
  class FuelGauge : public Adafruit_LC709203F
  {
  public:
    using Adafruit_LC709203F::readWord;
    using Adafruit_LC709203F::writeWord;
  };

  FuelGauge gauge;
  TwoWire *bus = &Wire;
  bool initialized = false;
  bool hasPreviousReading = false;
  uint16_t previousPercentageTenths = 0;
  bool charging = false;
};
