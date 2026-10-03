#include <string.h>
#include "HmiFrameReassembler.h"

bool HmiFrameReassembler::addFrame(const uint8_t *frame, size_t frameLength)
{
  HmiMessageType frameType;
  uint8_t chunkIndex;
  uint8_t chunkCount;
  uint16_t bodyLength;
  if (!HmiEnvelope::readHeader(frame, frameLength, frameType, chunkIndex, chunkCount, bodyLength))
  {
    return false;
  }

  const uint8_t *body = frame + HmiEnvelope::HEADER_SIZE;

  if (chunkIndex == 0)
  {
    type = frameType;
    messageLength = 0; // starting a fresh reassembly
  }

  if (messageLength + bodyLength > MAX_MESSAGE_SIZE)
  {
    return false; // larger than this stub supports - drop rather than overflow the buffer
  }

  memcpy(message + messageLength, body, bodyLength);
  messageLength += bodyLength;

  return (size_t)(chunkIndex + 1) >= chunkCount;
}
