#include <assert.h>
#include <stdio.h>
#include <string.h>
#include <vector>
#include "../src/protocol/HmiFrameReassembler.h"
#include "../src/protocol/HmiCommandEncoder.h"

using Bytes = std::vector<uint8_t>;

static Bytes frame(HmiMessageType type, uint8_t index, uint8_t count, uint32_t id, const Bytes &body)
{
  Bytes result(HmiEnvelope::HEADER_SIZE + body.size());
  HmiEnvelope::writeHeader(result.data(), type, index, count, (uint16_t)body.size(), id);
  memcpy(result.data() + HmiEnvelope::HEADER_SIZE, body.data(), body.size());
  return result;
}

static bool add(HmiFrameReassembler &assembler, const Bytes &bytes)
{
  return assembler.addFrame(bytes.data(), bytes.size());
}

static Bytes message(const HmiFrameReassembler &assembler)
{
  return Bytes(assembler.getMessage(), assembler.getMessage() + assembler.getMessageLength());
}

static void interleavedTypes()
{
  HmiFrameReassembler assembler;
  assert(!add(assembler, frame(HmiMessageType::ThermostatStateChunk, 0, 2, 10, {1, 2})));
  assert(add(assembler, frame(HmiMessageType::EnvironmentReadingChanged, 0, 1, 11, {3, 4})));
  assert(assembler.getType() == HmiMessageType::EnvironmentReadingChanged);
  assert(message(assembler) == Bytes({3, 4}));
  assert(add(assembler, frame(HmiMessageType::ThermostatStateChunk, 1, 2, 10, {5, 6})));
  assert(assembler.getType() == HmiMessageType::ThermostatStateChunk);
  assert(message(assembler) == Bytes({1, 2, 5, 6}));
}

static void allTypesCanBePending()
{
  HmiFrameReassembler assembler;
  const HmiMessageType types[] = {HmiMessageType::ThermostatStateChunk, HmiMessageType::ControlStateChanged,
                                  HmiMessageType::EnvironmentReadingChanged, HmiMessageType::PreferencesChanged};
  for (auto type : types)
    assert(!add(assembler, frame(type, 0, 2, (uint32_t)type, {(uint8_t)type})));
  for (auto type : types)
  {
    assert(add(assembler, frame(type, 1, 2, (uint32_t)type, {42})));
    assert(assembler.getType() == type);
    assert(message(assembler) == Bytes({(uint8_t)type, 42}));
  }
}

static void gapsAndDuplicatesInvalidate()
{
  HmiFrameReassembler assembler;
  auto type = HmiMessageType::ThermostatStateChunk;
  assert(!add(assembler, frame(type, 2, 3, 10, {3})));
  assert(!add(assembler, frame(type, 0, 3, 10, {1})));
  assert(!add(assembler, frame(type, 2, 3, 10, {3})));
  assert(!add(assembler, frame(type, 1, 3, 10, {2})));
  assert(!add(assembler, frame(type, 2, 3, 10, {3})));
  assert(assembler.getMessageLength() == 0);
  assert(!add(assembler, frame(type, 0, 3, 11, {1})));
  assert(!add(assembler, frame(type, 1, 3, 11, {2})));
  assert(!add(assembler, frame(type, 1, 3, 11, {2})));
  assert(!add(assembler, frame(type, 2, 3, 11, {3})));
  assert(!add(assembler, frame(type, 0, 2, 12, {4})));
  assert(add(assembler, frame(type, 1, 2, 12, {5})));
  assert(message(assembler) == Bytes({4, 5}));
  assert(!add(assembler, frame(type, 1, 2, 12, {5})));
}

static void changedIdsAndCountsInvalidate()
{
  HmiFrameReassembler assembler;
  auto type = HmiMessageType::ThermostatStateChunk;
  assert(!add(assembler, frame(type, 0, 2, 10, {1})));
  assert(!add(assembler, frame(type, 1, 2, 11, {2})));
  assert(!add(assembler, frame(type, 1, 2, 10, {2})));
  assert(!add(assembler, frame(type, 0, 2, 12, {1})));
  assert(!add(assembler, frame(type, 1, 3, 12, {2})));
  assert(!add(assembler, frame(type, 2, 3, 12, {3})));
  assert(!add(assembler, frame(type, 0, 2, 13, {4})));
  assert(!add(assembler, frame(type, 0, 2, 14, {5})));
  assert(add(assembler, frame(type, 1, 2, 14, {6})));
  assert(message(assembler) == Bytes({5, 6}));
}

static void malformedFramesInvalidateOnlyTheirType()
{
  const HmiMessageType type = HmiMessageType::ThermostatStateChunk;
  std::vector<Bytes> malformed = {
      frame(type, 0, 0, 10, {}), frame(type, 2, 2, 10, {}), frame(type, 0, 1, 10, {42}),
      frame(type, 1, 2, 10, {42}), Bytes({(uint8_t)type})};
  malformed[2].push_back(99);
  malformed[3].pop_back();
  for (const auto &bytes : malformed)
  {
    HmiFrameReassembler assembler;
    assert(!add(assembler, frame(type, 0, 2, 10, {1})));
    assert(!add(assembler, frame(HmiMessageType::ControlStateChanged, 0, 2, 20, {3})));
    assert(!add(assembler, bytes));
    assert(!add(assembler, frame(type, 1, 2, 10, {2})));
    assert(add(assembler, frame(HmiMessageType::ControlStateChanged, 1, 2, 20, {4})));
    assert(message(assembler) == Bytes({3, 4}));
  }
}

static void sizeLimitAndRecovery()
{
  HmiFrameReassembler assembler;
  auto type = HmiMessageType::ThermostatStateChunk;
  const size_t limit = HmiFrameReassembler::MAX_MESSAGE_SIZE;
  assert(!add(assembler, frame(type, 0, 2, 10, Bytes(limit - 1, 42))));
  assert(!add(assembler, frame(type, 1, 2, 10, {1, 2})));
  assert(assembler.getMessageLength() == 0);
  assert(!add(assembler, frame(type, 0, 2, 11, Bytes(limit - 1, 42))));
  assert(add(assembler, frame(type, 1, 2, 11, {42})));
  assert(message(assembler) == Bytes(limit, 42));
  assert(!add(assembler, frame(type, 0, 1, 12, Bytes(limit + 1, 42))));
  assert(add(assembler, frame(type, 0, 1, 13, {43})));
  assert(message(assembler) == Bytes({43}));
}

static void emptyUnknownAndMaximumCount()
{
  HmiFrameReassembler assembler;
  assert(!assembler.addFrame(nullptr, 0));
  assert(!add(assembler, frame(HmiMessageType::SetMode, 0, 1, 10, {1})));
  assert(add(assembler, frame(HmiMessageType::PreferencesChanged, 0, 1, 11, {})));
  assert(assembler.getMessageLength() == 0);
  for (uint16_t index = 0; index < 255; index++)
  {
    bool completed = add(assembler, frame(HmiMessageType::ThermostatStateChunk, (uint8_t)index, 255, 12, {42}));
    assert(completed == (index == 254));
  }
  assert(message(assembler) == Bytes(255, 42));
}

static void headerAndUplinkIds()
{
  auto bytes = frame(HmiMessageType::PreferencesChanged, 0, 1, 0x12345678, {1, 0});
  assert(bytes == Bytes({5, 0, 1, 2, 0, 0x78, 0x56, 0x34, 0x12, 1, 0}));
  uint8_t buffer[64];
  HmiMessageType type;
  uint8_t index, count;
  uint16_t length;
  uint32_t firstId, secondId;
  size_t size = HmiCommandEncoder::encodeSetMode(RunMode::Heating, buffer, sizeof(buffer));
  assert(HmiEnvelope::readHeader(buffer, size, type, index, count, length, firstId));
  assert(type == HmiMessageType::SetMode && index == 0 && count == 1 && length == 1);
  assert(buffer[HmiEnvelope::HEADER_SIZE] == (uint8_t)RunMode::Heating);
  size = HmiCommandEncoder::encodeRequestState(buffer, sizeof(buffer));
  assert(HmiEnvelope::readHeader(buffer, size, type, index, count, length, secondId));
  assert(type == HmiMessageType::RequestState && length == 0 && secondId != firstId);
  assert(HmiCommandEncoder::encodeSetMode(RunMode::Heating, buffer, HmiEnvelope::HEADER_SIZE) == 0);
}

int main()
{
  interleavedTypes();
  allTypesCanBePending();
  gapsAndDuplicatesInvalidate();
  changedIdsAndCountsInvalidate();
  malformedFramesInvalidateOnlyTheirType();
  sizeLimitAndRecovery();
  emptyUnknownAndMaximumCount();
  headerAndUplinkIds();
  puts("All 8 firmware protocol test groups passed.");
}