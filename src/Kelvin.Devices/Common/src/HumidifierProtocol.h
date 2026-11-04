#pragma once

#include <stdint.h>

// Mirrors Kelvin.Server's Models/FrameTags.cs (FrameTags.Humidifier) - keep the byte values in sync.
static const uint8_t humidifierFrameTag[4] = {0x4B, 0x48, 0x55, 0x4D}; // "KHUM"

// Mirrors Kelvin.Server's Features/Humidifiers/HumidifierProtocol.cs - keep in sync. Each frame (after the tag)
// is this type byte followed by a versioned CBOR map; every message fits in a single ESP-NOW frame.
enum class HumidifierMessageType : uint8_t
{
  // Humidifier -> Server. Body: {0: version(1), 1: relayOn(bool)}.
  Ping = 1,

  // Server -> Humidifier. Body: {0: version(1), 1: enabled(bool)}.
  State = 2,
};
