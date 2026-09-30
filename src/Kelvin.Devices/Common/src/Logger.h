#pragma once

#include <Arduino.h>
#include "Config.h"

// Compiles away entirely (no Serial calls, no arg evaluation) when DEBUG is not defined.
#if defined(DEBUG)
// Serial is the native USB CDC port on this board (cdc_on_boot=1); !Serial only clears once a USB
// host opens it, which never happens on battery, so this must time out instead of waiting forever.
// waitForHost should be false for latency-sensitive wakes (e.g. button) so they aren't delayed.
#define LOG_BEGIN(baud, waitForHost)                     \
  do                                                     \
  {                                                      \
    Serial.begin(baud);                                  \
    if (waitForHost)                                     \
    {                                                    \
      unsigned long logBeginStart = millis();            \
      while (!Serial && millis() - logBeginStart < 1000) \
      {                                                  \
        delay(100);                                      \
      }                                                  \
    }                                                    \
  } while (0)
#define LOG_PRINT(...) Serial.print(__VA_ARGS__)
#define LOG_PRINTLN(...) Serial.println(__VA_ARGS__)
#define LOG_PRINTF(...) Serial.printf(__VA_ARGS__)
// Deep sleep cuts power to the USB CDC peripheral mid-transfer, so drain the TX buffer first.
#define LOG_FLUSH() \
  do                \
  {                 \
    Serial.flush(); \
    delay(20);      \
  } while (0)
#else
#define LOG_BEGIN(baud, waitForHost) \
  do                                 \
  {                                  \
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
#define LOG_FLUSH() \
  do                \
  {                 \
  } while (0)
#endif
