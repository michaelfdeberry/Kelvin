#include "IoExtension.h"
#include "Config.h"

void IoExtension::begin(TwoWire &wire)
{
  bus = &wire;
}

void IoExtension::initialize()
{
  writeRegister(MODE_REGISTER, 0xFF); // all pins output, matching the official demo's IO_EXTENSION_Init()
  writeRegister(OUTPUT_REGISTER, outputValue);
}

void IoExtension::setOutput(uint8_t pin, bool value)
{
  if (value)
  {
    outputValue |= (1 << pin);
  }
  else
  {
    outputValue &= ~(1 << pin);
  }
  writeRegister(OUTPUT_REGISTER, outputValue);
}

void IoExtension::writeRegister(uint8_t reg, uint8_t value)
{
  bus->beginTransmission(IO_EXPANDER_I2C_ADDRESS);
  bus->write(reg);
  bus->write(value);
  bus->endTransmission();
}
