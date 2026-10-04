#include "Gt911Touch.h"

bool Gt911Touch::begin(TwoWire &wire, uint8_t i2cAddress)
{
  bus = &wire;
  address = i2cAddress;
  bus->beginTransmission(address);
  return bus->endTransmission() == 0;
}

bool Gt911Touch::read(int16_t &x, int16_t &y)
{
  uint8_t status = 0;
  if (!readRegister(STATUS_REGISTER, &status, 1))
  {
    return pressed;
  }

  // Bit 7 = a new frame is buffered; without it, the previous state still stands.
  if ((status & 0x80) == 0)
  {
    if (pressed)
    {
      x = lastX;
      y = lastY;
    }
    return pressed;
  }

  uint8_t touchCount = status & 0x0F;
  pressed = touchCount > 0;
  if (pressed)
  {
    uint8_t point[6];
    if (readRegister(FIRST_POINT_REGISTER, point, sizeof(point)))
    {
      lastX = point[1] | (point[2] << 8);
      lastY = point[3] | (point[4] << 8);
    }
  }

  writeRegister(STATUS_REGISTER, 0); // required to let the controller buffer the next frame

  if (pressed)
  {
    x = lastX;
    y = lastY;
  }
  return pressed;
}

bool Gt911Touch::readRegister(uint16_t reg, uint8_t *buffer, size_t length)
{
  bus->beginTransmission(address);
  bus->write(reg >> 8);
  bus->write(reg & 0xFF);
  if (bus->endTransmission(false) != 0)
  {
    return false;
  }
  if (bus->requestFrom(address, length) != length)
  {
    return false;
  }
  for (size_t i = 0; i < length; i++)
  {
    buffer[i] = bus->read();
  }
  return true;
}

void Gt911Touch::writeRegister(uint16_t reg, uint8_t value)
{
  bus->beginTransmission(address);
  bus->write(reg >> 8);
  bus->write(reg & 0xFF);
  bus->write(value);
  bus->endTransmission();
}
