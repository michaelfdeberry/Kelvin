#pragma once

#include <stddef.h>
#include <stdint.h>
#include <HumidifierProtocol.h>

// Envelope + CBOR bodies for the messages in Common/src/HumidifierProtocol.h.
namespace HumidifierMessages
{
  // Encodes a CheckIn or StateAck. Returns the encoded length, or 0 if the buffer is too small.
  size_t encodeRelayState(uint8_t *buffer, size_t size, HumidifierMessageType type, bool relayOn);

  bool decodeState(const uint8_t *frame, size_t length, bool &enabled);
}
