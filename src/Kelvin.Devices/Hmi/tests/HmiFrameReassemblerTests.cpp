#include <assert.h>
#include <math.h>
#include <stdio.h>
#include <string.h>
#include <vector>
#include <functional>
#include "../src/protocol/HmiFrameReassembler.h"
#include "../src/protocol/HmiCommandEncoder.h"
#include "../src/protocol/EnvironmentReadingParser.h"
#include "../src/protocol/HmiCbor.h"
#include "../src/protocol/ThermostatStateParser.h"
#include "../src/protocol/ControlStateParser.h"
#include "../src/protocol/PreferencesParser.h"

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
  assert(type == HmiMessageType::SetMode && index == 0 && count == 1 && length == 5);
  HmiCbor::Map command;
  uint32_t mode = 0;
  assert(command.parse(buffer + HmiEnvelope::HEADER_SIZE, length) && command.unsignedValue(1, mode, 4));
  assert(mode == (uint8_t)RunMode::Heating);
  size = HmiCommandEncoder::encodeRequestState(buffer, sizeof(buffer));
  assert(HmiEnvelope::readHeader(buffer, size, type, index, count, length, secondId));
  assert(type == HmiMessageType::RequestState && length == 3 && secondId != firstId);
  assert(HmiCommandEncoder::encodeSetMode(RunMode::Heating, buffer, HmiEnvelope::HEADER_SIZE) == 0);
}

static Bytes cborReading()
{
  return {0xA4, 0x00, 0x01, 0x01, 0xF9, 0x4D, 0x60, 0x02, 0xF9, 0x52, 0x40, 0x03, 0xF9, 0x5E, 0x40};
}

static void cborGoldenVectorAndExtensions()
{
  EnvironmentAverageReading reading;
  auto bytes = cborReading();
  assert(EnvironmentReadingParser::parse(bytes.data(), bytes.size(), reading));
  assert(reading.temperatureC == 21.5f && reading.humidityPercentage == 50.0f && reading.co2LevelPpm == 400.0f);
  Bytes reordered = {0xA5, 0x03, 0xF9, 0x5E, 0x40, 0x18, 0x64, 0x82, 0x01, 0x02,
                     0x00, 0x01, 0x02, 0xF9, 0x52, 0x40, 0x01, 0xF9, 0x4D, 0x60};
  assert(EnvironmentReadingParser::parse(reordered.data(), reordered.size(), reading));
  assert(reading.temperatureC == 21.5f && reading.co2LevelPpm == 400.0f);
}

static void cborFloatWidths()
{
  EnvironmentAverageReading reading;
  Bytes single = {0xA4, 0x00, 0x01, 0x01, 0xFA, 0x41, 0xAC, 0x00, 0x00,
                  0x02, 0xFA, 0x42, 0x48, 0x00, 0x00, 0x03, 0xFA, 0x43, 0xC8, 0x00, 0x00};
  assert(EnvironmentReadingParser::parse(single.data(), single.size(), reading));
  assert(reading.temperatureC == 21.5f && reading.humidityPercentage == 50.0f && reading.co2LevelPpm == 400.0f);
  Bytes wide = {0xA4, 0x00, 0x01, 0x01, 0xFB, 0x40, 0x35, 0x80, 0, 0, 0, 0, 0,
                0x02, 0xF9, 0x52, 0x40, 0x03, 0xF9, 0x5E, 0x40};
  assert(EnvironmentReadingParser::parse(wide.data(), wide.size(), reading));
  assert(reading.temperatureC == 21.5f);
  auto small = cborReading();
  small[5] = 0;
  small[6] = 1;
  assert(EnvironmentReadingParser::parse(small.data(), small.size(), reading));
  assert(reading.temperatureC == ldexpf(1.0f, -24));
  small[5] = 0x80;
  small[6] = 0;
  assert(EnvironmentReadingParser::parse(small.data(), small.size(), reading));
  assert(reading.temperatureC == 0 && signbit(reading.temperatureC));
}

static void cborMalformedDataIsAtomic()
{
  auto golden = cborReading();
  std::vector<Bytes> invalid;
  for (size_t size = 0; size < golden.size(); size++)
    invalid.push_back(Bytes(golden.begin(), golden.begin() + size));
  auto version = golden;
  version[2] = 2;
  invalid.push_back(version);
  auto duplicate = golden;
  duplicate[7] = 1;
  invalid.push_back(duplicate);
  auto infinity = golden;
  infinity[5] = 0x7C;
  infinity[6] = 0;
  invalid.push_back(infinity);
  infinity[6] = 1;
  invalid.push_back(infinity);
  auto trailing = golden;
  trailing.push_back(0);
  invalid.push_back(trailing);
  auto missing = golden;
  missing[0] = 0xA3;
  missing.resize(missing.size() - 4);
  invalid.push_back(missing);
  invalid.push_back(Bytes(12, 0));
  auto indefinite = golden;
  indefinite[0] = 0xBF;
  indefinite.push_back(0xFF);
  invalid.push_back(indefinite);
  auto unknownDuplicate = golden;
  unknownDuplicate[0] = 0xA6;
  unknownDuplicate.insert(unknownDuplicate.end(), {0x18, 0x64, 0xF6, 0x18, 0x64, 0xF6});
  invalid.push_back(unknownDuplicate);
  auto deep = golden;
  deep[0] = 0xA5;
  deep.insert(deep.end(), {0x18, 0x64, 0x81, 0x81, 0x81, 0x81, 0x81, 0x00});
  invalid.push_back(deep);
  for (const auto &bytes : invalid)
  {
    EnvironmentAverageReading reading;
    reading.temperatureC = 123;
    reading.humidityPercentage = 45;
    reading.co2LevelPpm = 678;
    assert(!EnvironmentReadingParser::parse(bytes.data(), bytes.size(), reading));
    assert(reading.temperatureC == 123 && reading.humidityPercentage == 45 && reading.co2LevelPpm == 678);
  }
}

static Bytes thermostatVector()
{
  return {0xA8, 0, 1, 1, 2, 2, 0xF5, 3, 0xF9, 0x38, 0, 4, 0xF6, 5, 0xF6,
          6, 0x81, 0xA2, 1, 0, 2, 0xF9, 0x4D, 0x80,
          7, 0x81, 0xA5, 1, 0x50, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
          2, 0, 3, 0x19, 0x01, 0x68, 4, 0x19, 0x01, 0xA4, 5, 0xF9, 0x4D, 0x80};
}

static void allDownlinkStates()
{
  auto bytes = thermostatVector();
  ThermostatState state;
  assert(ThermostatStateParser::parse(bytes.data(), bytes.size(), state));
  assert(state.mode == RunMode::Heating && state.fanEnabled && state.hysteresisC == 0.5f);
  assert(!state.hasHeatingLockout && !state.hasCoolingLockout);
  assert(state.setPointCount == 1 && state.setPoints[0].targetTemperatureC == 22);
  assert(state.scheduleCount == 1 && state.schedules[0].startMinutes == 360 && state.schedules[0].endMinutes == 420);
  for (size_t index = 0; index < 16; index++)
    assert(state.schedules[0].id[index] == index);
  Bytes control = {0xA6, 0, 1, 1, 3, 2, 0xF9, 0x4D, 0x60, 3, 0xF9, 0x4D, 0x80, 4, 0xF6, 5, 0xF6};
  ControlCallState call;
  assert(ControlStateParser::parse(control.data(), control.size(), call));
  assert(call.state == ControlState::Heating && call.hasEnvironmentTemperature && call.environmentTemperatureC == 21.5f);
  assert(call.hasTargetTemperature && call.targetTemperatureC == 22 && !call.hasHumidity && !call.hasCO2);
  Bytes preferences = {0xA3, 0, 1, 1, 1, 2, 1};
  SystemPreferences settings;
  assert(PreferencesParser::parse(preferences.data(), preferences.size(), settings));
  assert(settings.temperatureUnit == TemperatureUnit::Fahrenheit && settings.timeFormat == TimeFormat::Hour12);
}

static void downlinkFailuresAreAtomic()
{
  auto bytes = thermostatVector();
  for (size_t length = 0; length < bytes.size(); length++)
  {
    ThermostatState state;
    state.hysteresisC = 123;
    assert(!ThermostatStateParser::parse(bytes.data(), length, state));
    assert(state.hysteresisC == 123 && state.scheduleCount == 0);
  }
  bytes[bytes.size() - 2] = 0x7C;
  bytes[bytes.size() - 1] = 0;
  ThermostatState state;
  state.hysteresisC = 123;
  assert(!ThermostatStateParser::parse(bytes.data(), bytes.size(), state));
  assert(state.hysteresisC == 123);
  Bytes preferences = {0xA3, 0, 1, 1, 2, 2, 0};
  SystemPreferences settings;
  assert(!PreferencesParser::parse(preferences.data(), preferences.size(), settings));
}

static void everyUplinkAndSmallBuffer()
{
  uint8_t id[16] = {};
  float lockout = 26;
  using Encode = std::function<size_t(uint8_t *, size_t)>;
  std::vector<Encode> encoders = {
      [](uint8_t *buffer, size_t size)
      { return HmiCommandEncoder::encodeSensorReading(21.5f, 50, 75, buffer, size); },
      [](uint8_t *buffer, size_t size)
      { return HmiCommandEncoder::encodeSetMode(RunMode::Cooling, buffer, size); },
      [](uint8_t *buffer, size_t size)
      { return HmiCommandEncoder::encodeSetFanEnabled(true, buffer, size); },
      [](uint8_t *buffer, size_t size)
      { return HmiCommandEncoder::encodeSetSetPoint(RunType::Heating, 22, buffer, size); },
      [&](uint8_t *buffer, size_t size)
      { return HmiCommandEncoder::encodeUpsertSchedule(id, RunType::Heating, 360, 420, 22, buffer, size); },
      [&](uint8_t *buffer, size_t size)
      { return HmiCommandEncoder::encodeRemoveSchedule(id, buffer, size); },
      [&](uint8_t *buffer, size_t size)
      { return HmiCommandEncoder::encodeSetForecastLockouts(nullptr, &lockout, buffer, size); },
      [](uint8_t *buffer, size_t size)
      { return HmiCommandEncoder::encodeRequestState(buffer, size); }};
  for (const auto &encode : encoders)
  {
    uint8_t buffer[240];
    size_t length = encode(buffer, sizeof(buffer));
    assert(length > HmiEnvelope::HEADER_SIZE);
    HmiCbor::Map map;
    assert(map.parse(buffer + HmiEnvelope::HEADER_SIZE, length - HmiEnvelope::HEADER_SIZE));
    for (size_t capacity = 0; capacity < length; capacity++)
    {
      memset(buffer, 0xCC, sizeof(buffer));
      assert(encode(buffer, capacity) == 0);
      for (size_t index = capacity; index < sizeof(buffer); index++)
        assert(buffer[index] == 0xCC);
    }
  }
  uint8_t buffer[240];
  size_t length = HmiCommandEncoder::encodeSetFanEnabled(true, buffer, sizeof(buffer));
  assert(Bytes(buffer + HmiEnvelope::HEADER_SIZE, buffer + length) == Bytes({0xA2, 0, 1, 1, 0xF5}));
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
  cborGoldenVectorAndExtensions();
  cborFloatWidths();
  cborMalformedDataIsAtomic();
  allDownlinkStates();
  downlinkFailuresAreAtomic();
  everyUplinkAndSmallBuffer();
  puts("All 14 firmware protocol test groups passed.");
}