#pragma once

#include <stddef.h>
#include <stdint.h>
#include <HmiProtocol.h>

// Reassembles any Hmi downlink message (not just ThermostatStateChunk) via the shared HmiEnvelope header -
// a single-chunk message (chunkCount == 1) completes on the first call; a multi-chunk message accumulates
// across calls, keyed by message type.
//
// STUB LIMITATION: assumes chunks arrive in order (chunkIndex 0, 1, 2, ...) and that only one message is
// ever mid-reassembly at a time - holds for this simple point-to-point Gateway link.
class HmiFrameReassembler
{
public:
  // Comfortably covers mode/fan/hysteresis/lockouts/set points plus up to a few dozen schedules (see
  // ThermostatStateParser::MAX_SCHEDULES) - well above the server's per-chunk budget, so this is
  // reassembling at most a handful of chunks for the largest message type.
  static const size_t MAX_MESSAGE_SIZE = 2048;

  // Feeds one physical downlink frame (envelope header included, as delivered by Communicator::tryReceive).
  // Returns true once the final chunk has been received - getType()/getMessage()/getMessageLength() are
  // then valid for the completed message. Returns false both while a multi-chunk message is still in
  // progress and when the frame itself is malformed/truncated - callers only need to know "not ready yet".
  bool addFrame(const uint8_t *frame, size_t frameLength);

  HmiMessageType getType() const { return type; }
  const uint8_t *getMessage() const { return message; }
  size_t getMessageLength() const { return messageLength; }

private:
  HmiMessageType type = (HmiMessageType)0;
  uint8_t message[MAX_MESSAGE_SIZE];
  size_t messageLength = 0;
};
