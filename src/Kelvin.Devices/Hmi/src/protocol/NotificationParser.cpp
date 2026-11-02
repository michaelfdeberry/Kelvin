#include <string.h>
#include "HmiCbor.h"
#include "NotificationParser.h"

bool NotificationParser::parse(const uint8_t *body, size_t length, HmiNotification &result)
{
  HmiCbor::Map map;
  HmiNotification parsed;
  if (!map.parse(body, length) || !map.id(1, parsed.id) ||
      !map.text(2, parsed.message, sizeof(parsed.message)) ||
      !map.unsignedValue(3, parsed.type, 3) || !map.boolean(4, parsed.dismissible) ||
      !map.text(5, parsed.heading, sizeof(parsed.heading), true) ||
      !map.boolean(6, parsed.banner) || !map.boolean(8, parsed.persistent))
    return false;
  auto duration = map.find(7);
  if (!duration || (!cbor_value_is_null(duration) && !map.unsignedValue(7, parsed.duration, INT32_MAX)))
    return false;
  result = parsed;
  return true;
}

bool NotificationParser::parseDismiss(const uint8_t *body, size_t length, uint8_t id[16])
{
  HmiCbor::Map map;
  uint8_t parsed[16];
  if (!map.parse(body, length) || !map.id(1, parsed))
    return false;
  memcpy(id, parsed, sizeof(parsed));
  return true;
}