#pragma once

#include <stddef.h>
#include <stdint.h>
#include <HmiProtocol.h>

// Only Heating/Cooling set points exist (see RunType) - one of each, at most.
static const size_t MAX_SET_POINTS = 2;

// Generous headroom over any realistic schedule count - see HmiFrameReassembler::MAX_MESSAGE_SIZE for the
// buffer budget this was sized against.
static const size_t MAX_SCHEDULES = 32;

struct SetPointState
{
  RunType type;
  float targetTemperatureC;
};

struct ScheduleState
{
  uint8_t id[16];
  RunType type;
  uint16_t startMinutes;
  uint16_t endMinutes;
  float targetTemperatureC;
};

// The reassembled, structured form of a ThermostatStateChunk payload (see HmiProtocol.h for the wire
// layout this mirrors).
struct ThermostatState
{
  RunMode mode = RunMode::Disabled;
  bool fanEnabled = false;
  float hysteresisC = 0.0f;
  bool hasHeatingLockout = false;
  float heatingLockoutC = 0.0f;
  bool hasCoolingLockout = false;
  float coolingLockoutC = 0.0f;

  SetPointState setPoints[MAX_SET_POINTS];
  size_t setPointCount = 0;

  ScheduleState schedules[MAX_SCHEDULES];
  size_t scheduleCount = 0;

  const SetPointState *findSetPoint(RunType type) const;
};

namespace ThermostatStateParser
{
  // Parses the fully reassembled state blob (HmiFrameReassembler::getMessage()/getMessageLength()) into `outState`.
  // Returns false if the buffer is truncated/malformed. Schedules beyond MAX_SCHEDULES are read (to keep the
  // offset correct) but dropped rather than overflowing `outState.schedules`.
  bool parse(const uint8_t *data, size_t length, ThermostatState &outState);
}
