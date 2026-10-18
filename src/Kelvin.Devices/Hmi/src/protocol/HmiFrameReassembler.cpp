#include <string.h>
#include "HmiFrameReassembler.h"

HmiFrameReassembler::PendingMessage *HmiFrameReassembler::pendingFor(HmiMessageType messageType)
{
  switch (messageType)
  {
  case HmiMessageType::ThermostatStateChunk:
    return &pendingMessages[0];
  case HmiMessageType::ControlStateChanged:
    return &pendingMessages[1];
  case HmiMessageType::EnvironmentReadingChanged:
    return &pendingMessages[2];
  case HmiMessageType::PreferencesChanged:
    return &pendingMessages[3];
  default:
    return nullptr;
  }
}

bool HmiFrameReassembler::addFrame(const uint8_t *frame, size_t frameLength)
{
  type = (HmiMessageType)0;
  messageLength = 0;
  PendingMessage *pending = frameLength > 0 ? pendingFor((HmiMessageType)frame[0]) : nullptr;
  if (!pending)
    return false;

  HmiMessageType frameType;
  uint8_t chunkIndex;
  uint8_t chunkCount;
  uint16_t bodyLength;
  uint32_t messageId;
  if (!HmiEnvelope::readHeader(frame, frameLength, frameType, chunkIndex, chunkCount, bodyLength, messageId))
  {
    pending->length = 0;
    pending->chunkCount = 0;
    return false;
  }

  const uint8_t *body = frame + HmiEnvelope::HEADER_SIZE;

  if (chunkIndex == 0)
  {
    pending->length = 0;
    pending->chunkCount = chunkCount;
    pending->nextChunkIndex = 0;
    pending->messageId = messageId;
  }

  if (pending->messageId != messageId || pending->chunkCount != chunkCount || pending->nextChunkIndex != chunkIndex ||
      bodyLength > MAX_MESSAGE_SIZE - pending->length)
  {
    pending->length = 0;
    pending->chunkCount = 0;
    return false;
  }

  memcpy(pending->data + pending->length, body, bodyLength);
  pending->length += bodyLength;
  pending->nextChunkIndex++;

  if (pending->nextChunkIndex != pending->chunkCount)
    return false;

  type = frameType;
  messageLength = pending->length;
  memcpy(message, pending->data, messageLength);
  pending->length = 0;
  pending->chunkCount = 0;
  return true;
}
