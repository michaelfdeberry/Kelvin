#pragma once

#include <Arduino.h>
#include "Config.h"

// Compiles away entirely (no Serial calls, no arg evaluation) when DEBUG is not defined.
#if defined(DEBUG)
#define LOG_BEGIN(baud) \
  do                    \
  {                     \
    Serial.begin(baud); \
    while (!Serial)     \
    {                   \
      delay(100);       \
    }                   \
  } while (0)
#define LOG_PRINT(...) Serial.print(__VA_ARGS__)
#define LOG_PRINTLN(...) Serial.println(__VA_ARGS__)
#define LOG_PRINTF(...) Serial.printf(__VA_ARGS__)
#else
#define LOG_BEGIN(baud) \
  do                    \
  {                     \
  } while (0)
#define LOG_PRINT(...) \
  do                   \
  {                    \
  } while (0)
#define LOG_PRINTLN(...) \
  do                     \
  {                      \
  } while (0)
#define LOG_PRINTF(...) \
  do                    \
  {                     \
  } while (0)
#endif
