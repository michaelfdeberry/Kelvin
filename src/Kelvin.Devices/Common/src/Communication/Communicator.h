#pragma once

#include <cstddef>
#include <cstdint>

// Generic ESP-NOW link to the Gateway, shared by every device sketch. Every outgoing frame is prefixed
// with a 4-byte tag (see Kelvin.Server's Models/FrameTags.cs - keep them in sync) so the Gateway can relay
// it verbatim and the server can dispatch on the tag alone. Devices that only ever send (Node) can ignore
// tryReceive(); bidirectional devices (Hmi) poll it from loop() for downlink commands addressed to them.
class Communicator
{
public:
  static const size_t MAX_FRAME_CONTENT = 240;

  void begin(const uint8_t gatewayMac[6], const uint8_t frameTag[4]);
  void end();
  bool send(const void *payload, size_t payloadLength);

  // Copies the most recent inbound frame (tag already stripped, and already verified to match this
  // Communicator's own frameTag) into `buffer`. Returns false if nothing new has arrived since the last
  // call. This is a single-slot mailbox, not a queue - a frame received before the previous one is drained
  // is overwritten, which is fine for Hmi's use (only the latest downlink state/ack matters).
  bool tryReceive(uint8_t *buffer, size_t bufferSize, size_t &outLength);

private:
  uint8_t gatewayMacAddress[6] = {0};
};
