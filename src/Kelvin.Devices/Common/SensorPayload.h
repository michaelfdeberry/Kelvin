#pragma once

#include <stdint.h>

typedef struct sensor_payload
{
  float temperature;
  float humidity;
  uint16_t co2;
  float batteryLevel;
} sensor_payload;

// Prepended to every Node ESP-NOW frame so the Gateway can positively identify a reading instead of relying
// on frame length alone, now that other device types share the same radio channel.
static const uint8_t nodeFrameTag[4] = {0x4B, 0x4E, 0x4F, 0x44}; // "KNOD"
