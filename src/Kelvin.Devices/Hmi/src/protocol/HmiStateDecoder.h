#pragma once

#include <stddef.h>
#include <stdint.h>

// Reassembles ThermostatStateChunk messages (see Common's HmiProtocol.h) into the full encoded thermostat
// state blob. Parsing that blob's fields is left to the Ui layer (stubbed for now) - this class only
// handles the chunk transport.
//
// STUB LIMITATION: assumes chunks arrive in order (chunkIndex 0, 1, 2, ...), which holds for this simple
// point-to-point Gateway link but isn't a general-purpose reassembler - revisit if that ever changes.
class HmiStateDecoder
{
public:
  // Comfortably covers mode/fan/hysteresis/lockouts/set points plus up to a few dozen schedules (see
  // ThermostatStateParser::MAX_SCHEDULES) - well above the server's ~200 byte-per-chunk budget, so this is
  // reassembling at most a handful of chunks.
  static const size_t MAX_STATE_SIZE = 2048;

  // Feeds one ThermostatStateChunk's payload, i.e. everything after the HmiMessageType byte (starting at
  // chunkIndex). Returns true once the final chunk has been received and the full state is ready.
  bool addChunk(const uint8_t *chunkPayload, size_t chunkPayloadLength);

  const uint8_t *getState() const { return state; }
  size_t getStateLength() const { return stateLength; }

private:
  uint8_t state[MAX_STATE_SIZE];
  size_t stateLength = 0;
};
