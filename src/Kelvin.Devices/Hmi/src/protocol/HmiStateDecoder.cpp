#include <string.h>
#include "HmiStateDecoder.h"

bool HmiStateDecoder::addChunk(const uint8_t *chunkPayload, size_t chunkPayloadLength)
{
  if (chunkPayloadLength < 2)
  {
    return false;
  }

  uint8_t chunkIndex = chunkPayload[0];
  uint8_t chunkCount = chunkPayload[1];
  const uint8_t *data = chunkPayload + 2;
  size_t dataLength = chunkPayloadLength - 2;

  if (chunkIndex == 0)
  {
    stateLength = 0; // starting a fresh state sync
  }

  if (stateLength + dataLength > MAX_STATE_SIZE)
  {
    return false; // larger than this stub supports - drop rather than overflow the buffer
  }

  memcpy(state + stateLength, data, dataLength);
  stateLength += dataLength;

  return (size_t)(chunkIndex + 1) >= chunkCount;
}
