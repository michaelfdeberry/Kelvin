#pragma once

#include <stddef.h>
#include <stdint.h>

// Mirrors Kelvin.Protocol/DeviceEnvelope.cs - keep in sync. Every device message (after the frame tag), both
// directions, is framed with this 9-byte header: type(1), index(1), count(1), body length(2 LE), message ID(4 LE).
// Message types are scoped to the device's frame tag. All chunks of one message share an ID.
namespace DeviceEnvelope
{
  static const size_t HEADER_SIZE = 9;

  inline void writeHeader(uint8_t *buffer, uint8_t type, uint8_t chunkIndex, uint8_t chunkCount, uint16_t bodyLength, uint32_t messageId)
  {
    buffer[0] = type;
    buffer[1] = chunkIndex;
    buffer[2] = chunkCount;
    buffer[3] = (uint8_t)(bodyLength & 0xFF);
    buffer[4] = (uint8_t)((bodyLength >> 8) & 0xFF);
    buffer[5] = (uint8_t)(messageId & 0xFF);
    buffer[6] = (uint8_t)((messageId >> 8) & 0xFF);
    buffer[7] = (uint8_t)((messageId >> 16) & 0xFF);
    buffer[8] = (uint8_t)((messageId >> 24) & 0xFF);
  }

  // Validates chunk metadata and requires the physical frame to match its declared body length exactly.
  inline bool readHeader(const uint8_t *frame, size_t frameLength, uint8_t &type, uint8_t &chunkIndex, uint8_t &chunkCount, uint16_t &bodyLength, uint32_t &messageId)
  {
    if (frameLength < HEADER_SIZE)
    {
      return false;
    }

    type = frame[0];
    chunkIndex = frame[1];
    chunkCount = frame[2];
    bodyLength = (uint16_t)(frame[3] | (frame[4] << 8));
    messageId = (uint32_t)frame[5] | ((uint32_t)frame[6] << 8) |
                ((uint32_t)frame[7] << 16) | ((uint32_t)frame[8] << 24);
    return chunkCount > 0 && chunkIndex < chunkCount && frameLength == HEADER_SIZE + bodyLength;
  }
}
