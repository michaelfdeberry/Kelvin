#include <string.h>
#include "NotificationToasts.h"
#include "UiTheme.h"
#include "UiWidgets.h"
#include "../protocol/HmiCbor.h"
#include "../protocol/HmiCommandEncoder.h"

void NotificationToasts::begin(Communicator &communicatorRef)
{
  communicator = &communicatorRef;
  stack = lv_obj_create(lv_layer_top());
  lv_obj_remove_style_all(stack);
  lv_obj_set_size(stack, 480, LV_SIZE_CONTENT);
  lv_obj_set_style_max_height(stack, 430, 0);
  lv_obj_align(stack, LV_ALIGN_BOTTOM_RIGHT, -16, -16);
  lv_obj_set_flex_flow(stack, LV_FLEX_FLOW_COLUMN);
  lv_obj_set_style_pad_all(stack, 8, 0);
  lv_obj_set_style_pad_gap(stack, 12, 0);
  lv_obj_set_scroll_dir(stack, LV_DIR_VER);
  lv_obj_remove_flag(stack, LV_OBJ_FLAG_CLICKABLE);
  for (Slot &slot : slots)
    slot.owner = this;
}

void NotificationToasts::show(const HmiNotification &notification)
{
  dismiss(notification.id);
  Slot *target = &slots[4];
  if (!notification.banner)
  {
    target = &slots[0];
    for (size_t index = 0; index < 4; index++)
    {
      if (!slots[index].alert)
      {
        target = &slots[index];
        break;
      }
      if (slots[index].order < target->order)
        target = &slots[index];
    }
  }
  remove(*target, false);
  memcpy(target->id, notification.id, sizeof(target->id));
  target->persistent = notification.persistent;
  target->order = ++sequence;

  lv_color_t colors[4] = {UiTheme::accentInfo(), UiTheme::accentHeat(), UiTheme::accentSuccess(), UiTheme::accentDanger()};
  const char *icons[4] = {LV_SYMBOL_BELL, LV_SYMBOL_WARNING, LV_SYMBOL_OK, LV_SYMBOL_WARNING};
  lv_color_t accent = colors[notification.type];
  lv_obj_t *alert = lv_obj_create(notification.banner ? lv_layer_top() : stack);
  target->alert = alert;
  lv_obj_remove_style_all(alert);
  lv_obj_set_size(alert, LV_PCT(100), LV_SIZE_CONTENT);
  lv_obj_set_style_max_height(alert, notification.banner ? 150 : 200, 0);
  lv_obj_set_style_pad_all(alert, 16, 0);
  lv_obj_set_style_pad_gap(alert, 12, 0);
  lv_obj_set_style_radius(alert, notification.banner ? 0 : 4, 0);
  lv_obj_set_style_bg_color(alert, lv_color_mix(accent, UiTheme::bgPanel(), 40), 0);
  lv_obj_set_style_bg_opa(alert, LV_OPA_COVER, 0);
  lv_obj_set_style_border_color(alert, accent, 0);
  lv_obj_set_style_border_width(alert, 1, 0);
  lv_obj_set_style_shadow_width(alert, 12, 0);
  lv_obj_set_style_shadow_opa(alert, LV_OPA_30, 0);
  lv_obj_set_flex_flow(alert, LV_FLEX_FLOW_ROW);
  lv_obj_set_scroll_dir(alert, LV_DIR_VER);
  if (notification.banner)
    lv_obj_align(alert, LV_ALIGN_TOP_MID, 0, 0);

  lv_obj_t *badge = lv_label_create(alert);
  lv_label_set_text(badge, icons[notification.type]);
  lv_obj_set_style_text_font(badge, UiTheme::fontLabel(), 0);
  lv_obj_set_style_text_color(badge, accent, 0);

  lv_obj_t *content = lv_obj_create(alert);
  lv_obj_remove_style_all(content);
  lv_obj_set_size(content, 0, LV_SIZE_CONTENT);
  lv_obj_set_flex_grow(content, 1);
  lv_obj_set_flex_flow(content, LV_FLEX_FLOW_COLUMN);
  lv_obj_set_style_pad_gap(content, 6, 0);
  lv_obj_remove_flag(content, LV_OBJ_FLAG_SCROLLABLE);
  if (notification.heading[0])
  {
    lv_obj_t *heading = lv_label_create(content);
    lv_obj_set_width(heading, LV_PCT(100));
    lv_label_set_text(heading, notification.heading);
    lv_obj_set_style_text_font(heading, UiTheme::fontLabel(), 0);
    lv_obj_set_style_text_color(heading, accent, 0);
  }
  lv_obj_t *message = lv_label_create(content);
  lv_obj_set_width(message, LV_PCT(100));
  lv_label_set_text(message, notification.message);
  lv_obj_set_style_text_font(message, UiTheme::fontBody(), 0);
  lv_obj_set_style_text_color(message, UiTheme::textMain(), 0);

  if (notification.dismissible)
  {
    lv_obj_t *close = UiWidgets::createFlatButton(alert, LV_SYMBOL_CLOSE, 3);
    lv_obj_set_size(close, 40, 40);
    lv_obj_add_event_cb(close, handleClose, LV_EVENT_CLICKED, target);
  }
  if (notification.duration > 0)
    target->timer = lv_timer_create(handleTimeout, notification.duration, target);
  if (!notification.persistent && (lastRefreshRequest == 0 || lv_tick_elaps(lastRefreshRequest) >= 5000))
  {
    lastRefreshRequest = lv_tick_get();
    uint8_t buffer[Communicator::MAX_FRAME_CONTENT];
    size_t length = HmiCommandEncoder::encodeRequestState(buffer, sizeof(buffer));
    if (length > 0)
      communicator->send(buffer, length);
  }
}

void NotificationToasts::dismiss(const uint8_t id[16])
{
  for (Slot &slot : slots)
    if (slot.alert && memcmp(slot.id, id, sizeof(slot.id)) == 0)
      remove(slot, false);
}

void NotificationToasts::remove(Slot &slot, bool sendDismiss)
{
  if (slot.timer)
  {
    lv_timer_delete(slot.timer);
    slot.timer = nullptr;
  }
  if (slot.alert)
  {
    lv_obj_delete(slot.alert);
    slot.alert = nullptr;
    if (sendDismiss && slot.persistent)
    {
      uint8_t buffer[Communicator::MAX_FRAME_CONTENT];
      HmiCbor::Writer writer(buffer, sizeof(buffer), HmiMessageType::DismissNotification, 1);
      writer.id(1, slot.id);
      size_t length = writer.finish();
      if (length > 0)
        communicator->send(buffer, length);
    }
  }
}

void NotificationToasts::handleClose(lv_event_t *event)
{
  Slot *slot = (Slot *)lv_event_get_user_data(event);
  slot->owner->remove(*slot, true);
}

void NotificationToasts::handleTimeout(lv_timer_t *timer)
{
  Slot *slot = (Slot *)lv_timer_get_user_data(timer);
  slot->owner->remove(*slot, true);
}

void NotificationToasts::clearPersistent()
{
  for (Slot &slot : slots)
    if (slot.persistent)
      remove(slot, false);
}