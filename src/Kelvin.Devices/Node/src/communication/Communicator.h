#pragma once

// #include <esp_now.h>
#include "Config.h"

extern const char *gatewayMacAddress;

class Communicator
{
public:
  void begin();
  bool send(const void *payload);
};
