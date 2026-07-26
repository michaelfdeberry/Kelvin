#pragma once

#include <array>

// #include <esp_now.h>
#include "Config.h"

class Communicator
{
public:
  void begin();
  bool send(const void *payload);

private:
  uint8_t gatewayMacAddress[6] = {0};
};
