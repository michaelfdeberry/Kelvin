#pragma once

#include <lvgl.h>
#include <HmiProtocol.h>
#include <Communication/Communicator.h>
#include "../protocol/ThermostatStateParser.h"
#include "../protocol/ControlStateParser.h"
#include "../protocol/EnvironmentReadingParser.h"
#include "../io/IoExtension.h"
#include "../io/Gt911Touch.h"
#include "../display/RgbPanel.h"
#include "SettingsPage.h"

// Main screen: a header (status badge, battery), the current system-average temperature with humidity/CO2
// and the onboard sensor, a full-width set point slider showing where the current temperature sits relative
// to the set point(s), and a bottom bar with a segmented mode control, a fan toggle and a gear button that
// opens SettingsPage (schedules + forecast lockouts). No weather forecast (not supported on this panel).
//
// The RGB panel is driven directly via esp_lcd (RgbPanel); backlight, touch-reset and GT911 touch go through
// Wire (IoExtension/Gt911Touch).
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

  // level is a percentage, or -1 when unknown.
  void applyBattery(int level, bool externalPower);

  void onModeSelected(RunMode mode);
  void onFanToggled(bool enabled);
  void onSetPointAdjusted(RunType type, float targetTemperatureC);
  void onUpsertSchedule(const uint8_t *scheduleId, RunType type, uint16_t startMinutes, uint16_t endMinutes, float targetTemperatureC);
  void onRemoveSchedule(const uint8_t scheduleId[16]);

  // Pass nullptr for a lockout to clear it.
  void onSetForecastLockouts(const float *heatingLockoutC, const float *coolingLockoutC);

  void openSettings();
  void closeSettings();

private:
  struct ModeButtonBinding
  {
    Ui *ui;
    RunMode mode;
  };

  Communicator *communicator = nullptr;
  SettingsPage settings;
  lv_obj_t *mainScreen = nullptr;

  RgbPanel panel;
  Gt911Touch touch;
  IoExtension ioExtension;

  ThermostatState thermostatState;
  ControlCallState controlState;
  bool hasEnvironmentReading = false;
  float environmentTemperatureC = 0.0f;
  float environmentHumidityPercentage = 0.0f;

  // Header
  lv_obj_t *statusBadge = nullptr;
  lv_obj_t *batteryLabel = nullptr;

  // Readings
  lv_obj_t *currentTempLabel = nullptr;
  lv_obj_t *humidityLabel = nullptr;
  lv_obj_t *co2Label = nullptr;
  lv_obj_t *onboardLabel = nullptr;

  // Set point slider (single knob in Heat/Cool, two knobs - heat..cool - in Auto)
  lv_obj_t *setPointSlider = nullptr;
  lv_obj_t *setPointReadout = nullptr;
  lv_obj_t *currentMarker = nullptr;
  lv_obj_t *currentMarkerLabel = nullptr;
  bool sliderDragging = false;

  // Mode + fan + schedules controls
  static const size_t MODE_BUTTON_COUNT = 4;
  lv_obj_t *modeButtons[MODE_BUTTON_COUNT] = {nullptr, nullptr, nullptr, nullptr};
  ModeButtonBinding modeButtonBindings[MODE_BUTTON_COUNT];
  lv_obj_t *fanButton = nullptr;
  lv_obj_t *fanButtonLabel = nullptr;

  void buildHeader(lv_obj_t *parent);
  void buildReadings(lv_obj_t *parent);
  void buildSetPointSlider(lv_obj_t *parent);
  void buildControls(lv_obj_t *parent);

  void refreshDial();
  void refreshSetPointSlider();
  void refreshModeButtons();
  void refreshFanButton();
  void refreshSensorCard();

  void commitSliderSetPoints();
  float *findOrAddSetPoint(RunType type);

  static void flushCallback(lv_display_t *display, const lv_area_t *area, uint8_t *pixelMap);
  static void touchReadCallback(lv_indev_t *indev, lv_indev_data_t *data);

  static void handleModeButtonClicked(lv_event_t *e);
  static void handleFanButtonClicked(lv_event_t *e);
  static void handleSettingsButtonClicked(lv_event_t *e);
  static void handleSliderEvent(lv_event_t *e);
};
