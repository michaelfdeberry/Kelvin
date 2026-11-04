#pragma once

#include <stddef.h>
#include <stdint.h>

// CBOR bodies for the messages in Common/src/HumidifierProtocol.h.
namespace HumidifierMessages
{
  // Returns the encoded length, or 0 if the buffer is too small.
  size_t encodePing(uint8_t *buffer, size_t size, bool relayOn);

  bool decodeState(const uint8_t *frame, size_t length, bool &enabled);
}
