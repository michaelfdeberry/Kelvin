#pragma once

#include <lvgl.h>
#include <Communication/Communicator.h>
#include "../protocol/NotificationParser.h"

class NotificationToasts
{
public:
  void begin(Communicator &communicatorRef);
  void show(const HmiNotification &notification);
  void dismiss(const uint8_t id[16]);
  void clearPersistent();

private:
  struct Slot
  {
    NotificationToasts *owner = nullptr;
    lv_obj_t *alert = nullptr;
    lv_timer_t *timer = nullptr;
    uint8_t id[16] = {0};
    bool persistent = false;
    uint32_t order = 0;
  };

  Communicator *communicator = nullptr;
  lv_obj_t *stack = nullptr;
  Slot slots[5];
  uint32_t sequence = 0;
  uint32_t lastRefreshRequest = 0;

  void remove(Slot &slot, bool sendDismiss);
  static void handleClose(lv_event_t *event);
  static void handleTimeout(lv_timer_t *timer);
};