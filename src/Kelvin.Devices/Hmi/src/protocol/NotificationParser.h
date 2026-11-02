#pragma once

#include <stddef.h>
#include <stdint.h>

struct HmiNotification
{
  uint8_t id[16] = {0};
  char message[1025] = {0};
  char heading[129] = {0};
  uint32_t type = 0;
  uint32_t duration = 0;
  bool dismissible = false;
  bool banner = false;
  bool persistent = true;
};

namespace NotificationParser
{
  bool parse(const uint8_t *body, size_t length, HmiNotification &result);
  bool parseDismiss(const uint8_t *body, size_t length, uint8_t id[16]);
}