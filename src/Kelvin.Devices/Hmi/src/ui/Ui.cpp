#include "Ui.h"
#include "../protocol/HmiCommandEncoder.h"
#include <Logger.h>
#include "Config.h"

namespace
{
  // TODO: replace with a real LVGL draw buffer/rendering strategy once the RGB panel driver is wired up.
  // A small buffer is enough for lv_init()/lv_display_create() to succeed for this stub pass.
  lv_color_t drawBuffer[DISPLAY_HORIZONTAL_RESOLUTION * 10];
}

void Ui::flushCallback(lv_display_t *display, const lv_area_t *area, uint8_t *pixelMap)
{
  // TODO: push `pixelMap` to the real RGB panel (esp_lcd RGB bus) once the driver is confirmed/wired up.
  lv_display_flush_ready(display);
}

void Ui::touchReadCallback(lv_indev_t *indev, lv_indev_data_t *data)
{
  // TODO: read the real GT911 touch controller once its I2C wiring is confirmed.
  data->state = LV_INDEV_STATE_RELEASED;
}

void Ui::begin(Communicator &communicatorRef)
{
  communicator = &communicatorRef;

  lv_init();

  lv_display_t *display = lv_display_create(DISPLAY_HORIZONTAL_RESOLUTION, DISPLAY_VERTICAL_RESOLUTION);
  lv_display_set_flush_cb(display, flushCallback);
  lv_display_set_buffers(display, drawBuffer, nullptr, sizeof(drawBuffer), LV_DISPLAY_RENDER_MODE_PARTIAL);

  lv_indev_t *touch = lv_indev_create();
  lv_indev_set_type(touch, LV_INDEV_TYPE_POINTER);
  lv_indev_set_read_cb(touch, touchReadCallback);

  LOG_PRINTLN("LVGL initialized (display/touch drivers are stubbed - see Ui.cpp TODOs).");
}

void Ui::tick()
{
  lv_timer_handler();
}

void Ui::applyThermostatState(const uint8_t *state, size_t stateLength)
{
  // TODO: parse the layout documented in HmiProtocol.h and update real widgets once the UI is built.
  LOG_PRINTF("Received a %u byte thermostat state sync (not yet applied to any UI).\n", (unsigned)stateLength);
}

void Ui::onModeSelected(RunMode mode)
{
  if (communicator == nullptr)
  {
    return;
  }

  uint8_t buffer[Communicator::MAX_FRAME_CONTENT];
  size_t length = HmiCommandEncoder::encodeSetMode(mode, buffer, sizeof(buffer));
  if (length > 0)
  {
    communicator->send(buffer, length);
  }
}

void Ui::onFanToggled(bool enabled)
{
  if (communicator == nullptr)
  {
    return;
  }

  uint8_t buffer[Communicator::MAX_FRAME_CONTENT];
  size_t length = HmiCommandEncoder::encodeSetFanEnabled(enabled, buffer, sizeof(buffer));
  if (length > 0)
  {
    communicator->send(buffer, length);
  }
}

void Ui::onSetPointAdjusted(RunType type, float targetTemperatureC)
{
  if (communicator == nullptr)
  {
    return;
  }

  uint8_t buffer[Communicator::MAX_FRAME_CONTENT];
  size_t length = HmiCommandEncoder::encodeSetSetPoint(type, targetTemperatureC, buffer, sizeof(buffer));
  if (length > 0)
  {
    communicator->send(buffer, length);
  }
}
