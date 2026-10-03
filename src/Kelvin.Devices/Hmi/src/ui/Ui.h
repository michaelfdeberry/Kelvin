#pragma once

#include <lvgl.h>
#include <esp_display_panel.hpp>
#include <HmiProtocol.h>
#include <Communication/Communicator.h>
#include "../protocol/ThermostatStateParser.h"
#include "../protocol/ControlStateParser.h"
#include "../protocol/EnvironmentReadingParser.h"
#include "../io/IoExtension.h"
#include "ThermostatEditor.h"

// Main screen: a thermostat dial (current temperature + set point + HEATING/COOLING badge), mode/fan
// controls, and the onboard sensor's own temperature/humidity reading - the device-adjusted equivalent of
// Kelvin.Client's app-thermostat-control + app-sensor-card. No weather forecast (not supported on this
// panel). Tapping the pencil button opens ThermostatEditor for set points/schedules.
//
// The RGB panel/GT911 touch are driven via the official esp-arduino-libs/ESP32_Display_Panel library using
// a custom board config (esp_panel_board_custom_conf.h in this sketch folder - there's no built-in profile
// for this exact board revision, the ESP32-S3-Touch-LCD-7B). Backlight and touch-reset are driven directly
// through IoExtension instead of the library's own expander/backlight abstractions, since this board's IO
// extension chip doesn't match any expander chip the library supports - see begin() and
// esp_panel_board_custom_conf.h's USE_BACKLIGHT/USE_EXPANDER comments for why.
class Ui
{
public:
  void begin(Communicator &communicatorRef);
  void tick();

  // Called once a full ThermostatStateChunk set has been reassembled by HmiFrameReassembler.
  void applyThermostatState(const uint8_t *state, size_t stateLength);

  // Called whenever a ControlStateChanged message arrives (already parsed - it's small enough to never
  // need chunking, unlike the thermostat state).
  void applyControlState(const ControlCallState &state);

  // Called whenever an EnvironmentReadingChanged message arrives - the same system-wide average pushed to
  // web clients, far more often than ControlStateChanged, so this is what keeps the dial's reading live
  // in between HVAC state transitions.
  void applyEnvironmentAverage(const EnvironmentAverageReading &reading);

  // Called after every onboard sensor read (regardless of whether it was also sent upstream), so the
  // sensor card reflects the latest reading immediately rather than only when a send is due.
  void applyEnvironmentReading(float temperatureC, float humidityPercentage);

  void onModeSelected(RunMode mode);
  void onFanToggled(bool enabled);
  void onSetPointAdjusted(RunType type, float targetTemperatureC);
  void onUpsertSchedule(const uint8_t *scheduleId, RunType type, uint16_t startMinutes, uint16_t endMinutes, float targetTemperatureC);
  void onRemoveSchedule(const uint8_t scheduleId[16]);

private:
  struct ModeButtonBinding
  {
    Ui *ui;
    RunMode mode;
  };

  Communicator *communicator = nullptr;
  ThermostatEditor editor;

  esp_panel::board::Board *panelBoard = nullptr;
  esp_panel::drivers::LCD *lcd = nullptr;
  esp_panel::drivers::Touch *touch = nullptr;
  IoExtension ioExtension;

  ThermostatState thermostatState;
  ControlCallState controlState;
  bool hasEnvironmentReading = false;
  float environmentTemperatureC = 0.0f;
  float environmentHumidityPercentage = 0.0f;

  // Dial
  lv_obj_t *dialArc = nullptr;
  lv_obj_t *targetTempLabel = nullptr;
  lv_obj_t *currentTempLabel = nullptr;
  lv_obj_t *environmentSubtitleLabel = nullptr;
  lv_obj_t *statusBadge = nullptr;
  lv_obj_t *editButton = nullptr;

  // Mode + fan controls
  static const size_t MODE_BUTTON_COUNT = 4;
  lv_obj_t *modeButtons[MODE_BUTTON_COUNT] = {nullptr, nullptr, nullptr, nullptr};
  ModeButtonBinding modeButtonBindings[MODE_BUTTON_COUNT];
  lv_obj_t *fanButton = nullptr;
  lv_obj_t *fanButtonLabel = nullptr;

  // Onboard sensor card
  lv_obj_t *sensorValueLabel = nullptr;
  lv_obj_t *sensorSubtitleLabel = nullptr;

  void buildDial(lv_obj_t *parent);
  void buildControls(lv_obj_t *parent);
  void buildSensorCard(lv_obj_t *parent);

  void refreshDial();
  void refreshModeButtons();
  void refreshFanButton();
  void refreshSensorCard();

  static void flushCallback(lv_display_t *display, const lv_area_t *area, uint8_t *pixelMap);
  static void touchReadCallback(lv_indev_t *indev, lv_indev_data_t *data);

  static void handleModeButtonClicked(lv_event_t *e);
  static void handleFanButtonClicked(lv_event_t *e);
  static void handleEditButtonClicked(lv_event_t *e);
};
