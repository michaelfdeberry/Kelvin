#pragma once

#include <stdint.h>

typedef struct sensor_payload
{
  float temperature;
  float humidity;
  uint16_t co2;
  float batteryLevel;
} sensor_payload;

extern sensor_payload payload;
