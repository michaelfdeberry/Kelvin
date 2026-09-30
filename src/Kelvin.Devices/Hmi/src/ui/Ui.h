#pragma once

#include <lvgl.h>
#include <HmiProtocol.h>
#include <Communication/Communicator.h>

// LVGL scaffold for the panel. THIS PASS ONLY STUBS INTERACTIVITY: the display/touch drivers below are
// placeholders (see the TODOs in Ui.cpp) since the exact RGB bus + CH422G expander + GT911 touch wiring
// for the Waveshare ESP32-S3-Touch-LCD-7B hasn't been verified yet. The on*() handlers already build and
// send the real wire messages, though - wiring a real widget's event callback to one of them is the only
// remaining step once the UI itself is designed.
class Ui
{
public:
  void begin(Communicator &communicator);
  void tick();

  // Called once a full ThermostatStateChunk set has been reassembled by HmiStateDecoder.
  void applyThermostatState(const uint8_t *state, size_t stateLength);

  void onModeSelected(RunMode mode);
  void onFanToggled(bool enabled);
  void onSetPointAdjusted(RunType type, float targetTemperatureC);

private:
  Communicator *communicator = nullptr;

  static void flushCallback(lv_display_t *display, const lv_area_t *area, uint8_t *pixelMap);
  static void touchReadCallback(lv_indev_t *indev, lv_indev_data_t *data);
};
