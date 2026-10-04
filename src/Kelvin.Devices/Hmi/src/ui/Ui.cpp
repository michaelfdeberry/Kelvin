#include <stdio.h>
#include "Ui.h"
#include "UiTheme.h"
#include "UiWidgets.h"
#include "TemperatureFormat.h"
#include "../protocol/HmiCommandEncoder.h"
#include <Logger.h>
#include "Config.h"

namespace
{
  // One Ui per device, so a single file-scope pointer is simpler than threading a `Ui*` through every
  // LVGL callback's user_data - set once in Ui::begin(), read by flushCallback/touchReadCallback.
  Ui *uiInstance = nullptr;

  // A small bounce buffer is enough for LV_DISPLAY_RENDER_MODE_PARTIAL - the real pixels live in the
  // panel driver's own frame buffer (in PSRAM), this is just LVGL's staging area for one render pass.
  // LVGL asserts (and halts forever) if the buffer isn't LV_DRAW_BUF_ALIGN-aligned; lv_color_t is 3 bytes in
  // v9 regardless of color depth, so size it in RGB565 bytes (2 per pixel) instead.
  alignas(64) uint8_t drawBuffer[DISPLAY_HORIZONTAL_RESOLUTION * 10 * 2];

  // Briefly driven as an output during the touch-reset dance below (mirrors the official
  // ESP32-S3-Touch-LCD-7B demo's gt911.cpp) to select the GT911's 0x5D address, then released to an input.
  const int TOUCH_INT_PIN = 4;

  void formatFahrenheit(float celsius, char *buffer, size_t bufferSize)
  {
    snprintf(buffer, bufferSize, "%.1f\u00B0F", celsius * 9.0f / 5.0f + 32.0f);
  }

  const int CONTENT_PADDING = 28;
  const int KNOB_RADIUS = 32;             // slider knob is 2 * this wide
  const int KNOB_INSET = KNOB_RADIUS + 8; // the track is inset by this so the knob and its border never clip
  const int SLIDER_TRACK_HEIGHT = 20;
  const int SLIDER_Y = 64;
  const int SLIDER_AREA_HEIGHT = 150;
  const int SLIDER_MIN_F = 50;
  const int SLIDER_MAX_F = 90;

  int32_t clampSliderValue(int32_t fahrenheit)
  {
    return fahrenheit < SLIDER_MIN_F ? SLIDER_MIN_F : (fahrenheit > SLIDER_MAX_F ? SLIDER_MAX_F : fahrenheit);
  }

  // Single-knob mode only uses `highF`.
  void formatReadout(char *buffer, size_t bufferSize, bool range, int32_t lowF, int32_t highF)
  {
    if (range)
    {
      snprintf(buffer, bufferSize, "%d\u00B0 - %d\u00B0F", (int)lowF, (int)highF);
    }
    else
    {
      snprintf(buffer, bufferSize, "%d\u00B0F", (int)highF);
    }
  }

  void createStat(lv_obj_t *parent, const char *title, lv_obj_t **valueOut)
  {
    lv_obj_t *column = lv_obj_create(parent);
    lv_obj_remove_style_all(column);
    lv_obj_set_size(column, LV_SIZE_CONTENT, LV_SIZE_CONTENT);
    lv_obj_set_flex_flow(column, LV_FLEX_FLOW_COLUMN);

    lv_obj_t *titleLabel = lv_label_create(column);
    lv_label_set_text(titleLabel, title);
    lv_obj_set_style_text_font(titleLabel, UiTheme::fontBody(), 0);
    lv_obj_set_style_text_color(titleLabel, UiTheme::textMuted(), 0);

    *valueOut = lv_label_create(column);
    lv_obj_set_style_text_font(*valueOut, UiTheme::fontValue(), 0);
    lv_obj_set_style_text_color(*valueOut, UiTheme::textMain(), 0);
  }
}

void Ui::flushCallback(lv_display_t *display, const lv_area_t *area, uint8_t *pixelMap)
{
  if (uiInstance != nullptr)
  {
    uiInstance->panel.draw(area->x1, area->y1, area->x2 + 1, area->y2 + 1, pixelMap);
  }
  lv_display_flush_ready(display);
}

void Ui::touchReadCallback(lv_indev_t *indev, lv_indev_data_t *data)
{
  data->state = LV_INDEV_STATE_RELEASED;

  if (uiInstance == nullptr)
  {
    return;
  }

  int16_t x, y;
  if (uiInstance->touch.read(x, y))
  {
    data->point.x = x;
    data->point.y = y;
    data->state = LV_INDEV_STATE_PRESSED;
  }
}

void Ui::begin(Communicator &communicatorRef)
{
  communicator = &communicatorRef;
  uiInstance = this;

  // Wire.begin() has already run in Hmi.ino's setup() by this point - safe to talk to the IO extension
  // chip now. Reset GT911 touch via the IO extension's TOUCH_RESET pin before bringing up the panel,
  // matching the official 7B demo's gt911.cpp init dance (IO_EXTENSION_IO_1 low -> INT pin low -> IO_1
  // high).
  ioExtension.begin(Wire);
  ioExtension.initialize();
  LOG_PRINTLN("IO extension initialized.");
  ioExtension.setOutput(IoExtensionPin::TOUCH_RESET, false);
  pinMode(TOUCH_INT_PIN, OUTPUT);
  delay(100);
  digitalWrite(TOUCH_INT_PIN, LOW);
  delay(100);
  ioExtension.setOutput(IoExtensionPin::TOUCH_RESET, true);
  delay(50);
  pinMode(TOUCH_INT_PIN, INPUT);
  delay(150);

  if (!touch.begin(Wire))
  {
    LOG_PRINTLN("GT911 touch controller did not respond at 0x5D.");
  }

  LOG_PRINTLN("Starting RGB panel...");
  if (!panel.begin())
  {
    LOG_PRINTLN("Failed to initialize the RGB display panel.");
  }
  LOG_PRINTLN("RGB panel started, enabling backlight.");

  // Backlight is switched on only after the panel is up, to avoid a brief flash of garbage pixels.
  ioExtension.setOutput(IoExtensionPin::BACKLIGHT, true);

  lv_init();
  lv_tick_set_cb([]() -> uint32_t
                 { return millis(); });
  LOG_PRINTLN("LVGL core initialized.");

  lv_display_t *display = lv_display_create(DISPLAY_HORIZONTAL_RESOLUTION, DISPLAY_VERTICAL_RESOLUTION);
  lv_display_set_flush_cb(display, flushCallback);
  lv_display_set_buffers(display, drawBuffer, nullptr, sizeof(drawBuffer), LV_DISPLAY_RENDER_MODE_PARTIAL);

  lv_indev_t *touchIndev = lv_indev_create();
  lv_indev_set_type(touchIndev, LV_INDEV_TYPE_POINTER);
  lv_indev_set_read_cb(touchIndev, touchReadCallback);

  lv_obj_t *screen = lv_screen_active();
  mainScreen = screen;
  lv_obj_set_style_bg_color(screen, UiTheme::bgDark(), 0);
  lv_obj_set_style_bg_opa(screen, LV_OPA_COVER, 0);
  lv_obj_set_style_pad_all(screen, 0, 0);
  lv_obj_set_style_pad_gap(screen, 0, 0);
  lv_obj_remove_flag(screen, LV_OBJ_FLAG_SCROLLABLE);
  lv_obj_set_flex_flow(screen, LV_FLEX_FLOW_COLUMN);

  buildHeader(screen);

  lv_obj_t *content = lv_obj_create(screen);
  lv_obj_remove_style_all(content);
  lv_obj_set_width(content, LV_PCT(100));
  lv_obj_set_flex_grow(content, 1);
  lv_obj_set_style_pad_hor(content, CONTENT_PADDING, 0);
  lv_obj_set_style_pad_top(content, 14, 0);
  lv_obj_set_style_pad_bottom(content, 20, 0);
  lv_obj_set_flex_flow(content, LV_FLEX_FLOW_COLUMN);
  lv_obj_set_flex_align(content, LV_FLEX_ALIGN_SPACE_BETWEEN, LV_FLEX_ALIGN_START, LV_FLEX_ALIGN_START);

  buildReadings(content);
  LOG_PRINTLN("Readings built.");
  buildSetPointSlider(content);
  LOG_PRINTLN("Slider built.");
  buildControls(content);
  LOG_PRINTLN("Controls built.");

  settings.begin(*this);
  settings.applyState(thermostatState);
  LOG_PRINTLN("Settings built.");

  refreshDial();
  refreshModeButtons();
  refreshFanButton();
  refreshSensorCard();

  LOG_PRINTLN("LVGL initialized.");
}

void Ui::tick()
{
  lv_timer_handler();
}

void Ui::buildHeader(lv_obj_t *parent)
{
  lv_obj_t *bar = lv_obj_create(parent);
  lv_obj_remove_style_all(bar);
  lv_obj_set_size(bar, LV_PCT(100), 52);
  lv_obj_set_style_pad_hor(bar, 20, 0);
  lv_obj_set_style_pad_gap(bar, 16, 0);
  lv_obj_set_style_border_side(bar, LV_BORDER_SIDE_BOTTOM, 0);
  lv_obj_set_style_border_width(bar, 1, 0);
  lv_obj_set_style_border_color(bar, UiTheme::borderSubtle(), 0);
  lv_obj_set_flex_flow(bar, LV_FLEX_FLOW_ROW);
  lv_obj_set_flex_align(bar, LV_FLEX_ALIGN_START, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER);

  lv_obj_t *title = lv_label_create(bar);
  lv_label_set_text(title, "Kelvin");
  lv_obj_set_style_text_font(title, UiTheme::fontLabel(), 0);
  lv_obj_set_style_text_color(title, UiTheme::textMain(), 0);

  lv_obj_t *spacer = lv_obj_create(bar);
  lv_obj_remove_style_all(spacer);
  lv_obj_set_size(spacer, 0, 1);
  lv_obj_set_flex_grow(spacer, 1);

  statusBadge = lv_label_create(bar);
  lv_obj_set_style_text_font(statusBadge, UiTheme::fontBody(), 0);
  lv_obj_set_style_pad_hor(statusBadge, 14, 0);
  lv_obj_set_style_pad_ver(statusBadge, 4, 0);
  lv_obj_set_style_radius(statusBadge, 20, 0);
  lv_obj_set_style_border_width(statusBadge, 1, 0);

  batteryLabel = lv_label_create(bar);
  lv_obj_set_style_text_font(batteryLabel, UiTheme::fontBody(), 0);
  lv_obj_set_style_text_color(batteryLabel, UiTheme::textMuted(), 0);
  lv_label_set_text(batteryLabel, "");
}

void Ui::buildReadings(lv_obj_t *parent)
{
  lv_obj_t *row = lv_obj_create(parent);
  lv_obj_remove_style_all(row);
  lv_obj_set_size(row, LV_PCT(100), LV_SIZE_CONTENT);
  lv_obj_set_flex_flow(row, LV_FLEX_FLOW_ROW);
  lv_obj_set_flex_align(row, LV_FLEX_ALIGN_SPACE_BETWEEN, LV_FLEX_ALIGN_END, LV_FLEX_ALIGN_END);

  currentTempLabel = lv_label_create(row);
  lv_obj_set_style_text_font(currentTempLabel, UiTheme::fontHuge(), 0);
  lv_obj_set_style_text_color(currentTempLabel, UiTheme::textMain(), 0);

  lv_obj_t *stats = lv_obj_create(row);
  lv_obj_remove_style_all(stats);
  lv_obj_set_size(stats, LV_SIZE_CONTENT, LV_SIZE_CONTENT);
  lv_obj_set_style_pad_bottom(stats, 12, 0);
  lv_obj_set_style_pad_gap(stats, 36, 0);
  lv_obj_set_flex_flow(stats, LV_FLEX_FLOW_ROW);

  createStat(stats, "Avg humidity", &humidityLabel);
  createStat(stats, "Avg CO2", &co2Label);
  createStat(stats, "Onboard", &onboardLabel);
}

void Ui::buildSetPointSlider(lv_obj_t *parent)
{
  const int sliderWidth = DISPLAY_HORIZONTAL_RESOLUTION - 2 * CONTENT_PADDING - 2 * KNOB_INSET;

  lv_obj_t *area = lv_obj_create(parent);
  lv_obj_remove_style_all(area);
  lv_obj_set_size(area, LV_PCT(100), SLIDER_AREA_HEIGHT);
  lv_obj_remove_flag(area, LV_OBJ_FLAG_SCROLLABLE);

  // Where the current temperature sits on the same scale as the set point(s) - positioned in refreshSetPointSlider().
  currentMarker = lv_obj_create(area);
  lv_obj_remove_style_all(currentMarker);
  lv_obj_set_size(currentMarker, 2, 26);
  lv_obj_set_y(currentMarker, SLIDER_Y - 24);
  lv_obj_set_style_bg_color(currentMarker, UiTheme::textMuted(), 0);
  lv_obj_set_style_bg_opa(currentMarker, LV_OPA_COVER, 0);

  currentMarkerLabel = lv_label_create(area);
  lv_obj_set_style_text_font(currentMarkerLabel, UiTheme::fontBody(), 0);
  lv_obj_set_style_text_color(currentMarkerLabel, UiTheme::textMuted(), 0);

  setPointSlider = lv_slider_create(area);
  lv_obj_set_size(setPointSlider, sliderWidth, SLIDER_TRACK_HEIGHT);
  lv_obj_set_pos(setPointSlider, KNOB_INSET, SLIDER_Y);
  lv_slider_set_range(setPointSlider, SLIDER_MIN_F, SLIDER_MAX_F);
  lv_obj_set_ext_click_area(setPointSlider, 24);
  lv_obj_set_style_bg_color(setPointSlider, UiTheme::bgPanel(), LV_PART_MAIN);
  lv_obj_set_style_bg_opa(setPointSlider, LV_OPA_COVER, LV_PART_MAIN);
  lv_obj_set_style_bg_opa(setPointSlider, LV_OPA_COVER, LV_PART_INDICATOR);
  lv_obj_set_style_bg_color(setPointSlider, UiTheme::textMain(), LV_PART_KNOB);
  lv_obj_set_style_bg_opa(setPointSlider, LV_OPA_COVER, LV_PART_KNOB);
  lv_obj_set_style_border_width(setPointSlider, 6, LV_PART_KNOB);
  lv_obj_set_style_pad_all(setPointSlider, KNOB_RADIUS - SLIDER_TRACK_HEIGHT / 2, LV_PART_KNOB);
  lv_obj_add_event_cb(setPointSlider, handleSliderEvent, LV_EVENT_ALL, this);

  lv_obj_t *ends = lv_obj_create(area);
  lv_obj_remove_style_all(ends);
  lv_obj_set_size(ends, LV_PCT(100), LV_SIZE_CONTENT);
  lv_obj_set_y(ends, SLIDER_Y + KNOB_RADIUS + 14);
  lv_obj_set_style_pad_hor(ends, KNOB_INSET, 0);
  lv_obj_set_flex_flow(ends, LV_FLEX_FLOW_ROW);
  lv_obj_set_flex_align(ends, LV_FLEX_ALIGN_SPACE_BETWEEN, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER);

  char endText[16];
  lv_obj_t *minLabel = lv_label_create(ends);
  snprintf(endText, sizeof(endText), "%d\u00B0F", SLIDER_MIN_F);
  lv_label_set_text(minLabel, endText);
  lv_obj_set_style_text_font(minLabel, UiTheme::fontBody(), 0);
  lv_obj_set_style_text_color(minLabel, UiTheme::textMuted(), 0);

  setPointReadout = lv_label_create(ends);
  lv_obj_set_style_text_font(setPointReadout, UiTheme::fontLabel(), 0);

  lv_obj_t *maxLabel = lv_label_create(ends);
  snprintf(endText, sizeof(endText), "%d\u00B0F", SLIDER_MAX_F);
  lv_label_set_text(maxLabel, endText);
  lv_obj_set_style_text_font(maxLabel, UiTheme::fontBody(), 0);
  lv_obj_set_style_text_color(maxLabel, UiTheme::textMuted(), 0);
}

void Ui::buildControls(lv_obj_t *parent)
{
  lv_obj_t *bar = lv_obj_create(parent);
  lv_obj_remove_style_all(bar);
  lv_obj_set_size(bar, LV_PCT(100), 96);
  lv_obj_set_style_pad_gap(bar, 16, 0);
  lv_obj_set_flex_flow(bar, LV_FLEX_FLOW_ROW);

  // Modes are one segmented control: a shared rounded track with the selected segment filled in.
  lv_obj_t *segments = lv_obj_create(bar);
  lv_obj_remove_style_all(segments);
  lv_obj_set_size(segments, 0, LV_PCT(100));
  lv_obj_set_flex_grow(segments, 4);
  lv_obj_set_style_pad_all(segments, 6, 0);
  lv_obj_set_style_pad_gap(segments, 6, 0);
  lv_obj_set_style_radius(segments, 18, 0);
  lv_obj_set_style_bg_color(segments, UiTheme::bgPanel(), 0);
  lv_obj_set_style_bg_opa(segments, LV_OPA_COVER, 0);
  lv_obj_set_style_border_width(segments, 1, 0);
  lv_obj_set_style_border_color(segments, UiTheme::borderSubtle(), 0);
  lv_obj_set_flex_flow(segments, LV_FLEX_FLOW_ROW);

  static const char *labels[MODE_BUTTON_COUNT] = {"Auto", "Heat", "Cool", "Off"};
  static const RunMode modes[MODE_BUTTON_COUNT] = {RunMode::Automatic, RunMode::Heating, RunMode::Cooling, RunMode::Off};

  for (size_t i = 0; i < MODE_BUTTON_COUNT; i++)
  {
    modeButtons[i] = UiWidgets::createFlatButton(segments, labels[i], 13);
    lv_obj_set_flex_grow(modeButtons[i], 1);
    modeButtonBindings[i] = {this, modes[i]};
    lv_obj_add_event_cb(modeButtons[i], handleModeButtonClicked, LV_EVENT_CLICKED, &modeButtonBindings[i]);
  }

  fanButton = UiWidgets::createFlatButton(bar, "", 18, &fanButtonLabel);
  lv_obj_set_flex_grow(fanButton, 1);
  lv_obj_set_style_bg_opa(fanButton, LV_OPA_COVER, 0);
  lv_obj_set_style_border_width(fanButton, 1, 0);
  lv_obj_add_event_cb(fanButton, handleFanButtonClicked, LV_EVENT_CLICKED, this);

  lv_obj_t *gearLabel = nullptr;
  lv_obj_t *gearButton = UiWidgets::createFlatButton(bar, LV_SYMBOL_SETTINGS, 18, &gearLabel);
  lv_obj_set_size(gearButton, 96, LV_PCT(100));
  lv_obj_set_style_bg_color(gearButton, UiTheme::bgPanel(), 0);
  lv_obj_set_style_bg_opa(gearButton, LV_OPA_COVER, 0);
  lv_obj_set_style_border_width(gearButton, 1, 0);
  lv_obj_set_style_border_color(gearButton, UiTheme::borderSubtle(), 0);
  lv_obj_set_style_text_font(gearLabel, UiTheme::fontValue(), 0);
  lv_obj_set_style_text_color(gearLabel, UiTheme::textMuted(), 0);
  lv_obj_add_event_cb(gearButton, handleSettingsButtonClicked, LV_EVENT_CLICKED, this);
}

void Ui::refreshDial()
{
  lv_color_t accent = UiTheme::accentIdle();
  bool showBadge = false;
  const char *badgeText = "";

  if (controlState.state == ControlState::Heating)
  {
    accent = UiTheme::accentHeat();
    showBadge = true;
    badgeText = "HEATING";
  }
  else if (controlState.state == ControlState::Cooling)
  {
    accent = UiTheme::accentCool();
    showBadge = true;
    badgeText = "COOLING";
  }

  lv_obj_set_style_text_color(statusBadge, accent, 0);

  if (showBadge)
  {
    lv_label_set_text(statusBadge, badgeText);
    lv_obj_set_style_text_color(statusBadge, UiTheme::textMain(), 0);
    lv_obj_set_style_bg_color(statusBadge, accent, 0);
    lv_obj_set_style_bg_opa(statusBadge, LV_OPA_30, 0);
    lv_obj_set_style_border_color(statusBadge, accent, 0);
    lv_obj_remove_flag(statusBadge, LV_OBJ_FLAG_HIDDEN);
  }
  else
  {
    lv_obj_add_flag(statusBadge, LV_OBJ_FLAG_HIDDEN);
  }

  // The dial shows the system-wide average environment (every sensor, including this panel's own), not just
  // this device's local reading - that only ever appears in the "Onboard Sensor" card below.
  char buffer[32];
  if (controlState.hasEnvironmentTemperature)
  {
    formatFahrenheit(controlState.environmentTemperatureC, buffer, sizeof(buffer));
    lv_label_set_text(currentTempLabel, buffer);
  }
  else if (hasEnvironmentReading)
  {
    formatFahrenheit(environmentTemperatureC, buffer, sizeof(buffer));
    lv_label_set_text(currentTempLabel, buffer);
  }
  else
  {
    lv_label_set_text(currentTempLabel, "--");
  }

  // The hero temperature and these stats are the system-wide average (every sensor, including this panel's
  // own); only the "Onboard" stat is this device's local reading.
  if (controlState.hasHumidity)
  {
    snprintf(buffer, sizeof(buffer), "%.0f%%", controlState.humidityPercentage);
    lv_label_set_text(humidityLabel, buffer);
  }
  else
  {
    lv_label_set_text(humidityLabel, "--");
  }

  if (controlState.hasCO2)
  {
    snprintf(buffer, sizeof(buffer), "%.0f ppm", controlState.co2LevelPpm);
    lv_label_set_text(co2Label, buffer);
  }
  else
  {
    lv_label_set_text(co2Label, "--");
  }

  refreshSetPointSlider();
}

void Ui::refreshSetPointSlider()
{
  RunMode mode = thermostatState.mode;
  const SetPointState *heating = thermostatState.findSetPoint(RunType::Heating);
  const SetPointState *cooling = thermostatState.findSetPoint(RunType::Cooling);

  bool range = mode == RunMode::Automatic;
  const SetPointState *single = mode == RunMode::Cooling ? cooling : heating;
  bool usable = range ? (heating != nullptr && cooling != nullptr)
                      : ((mode == RunMode::Heating || mode == RunMode::Cooling) && single != nullptr);

  lv_color_t color = UiTheme::accentIdle();
  if (usable)
  {
    color = range ? UiTheme::accentSuccess() : (mode == RunMode::Cooling ? UiTheme::accentCool() : UiTheme::accentHeat());
  }
  lv_obj_set_style_bg_color(setPointSlider, color, LV_PART_INDICATOR);
  lv_obj_set_style_border_color(setPointSlider, color, LV_PART_KNOB);
  lv_obj_set_style_text_color(setPointReadout, lv_color_eq(color, UiTheme::accentIdle()) ? UiTheme::textMuted() : color, 0);
  lv_obj_set_style_opa(setPointSlider, usable ? LV_OPA_COVER : LV_OPA_40, 0);

  if (usable)
  {
    lv_obj_remove_state(setPointSlider, LV_STATE_DISABLED);
  }
  else
  {
    lv_obj_add_state(setPointSlider, LV_STATE_DISABLED);
  }

  lv_slider_set_mode(setPointSlider, range ? LV_SLIDER_MODE_RANGE : LV_SLIDER_MODE_NORMAL);

  // A drag in progress owns the knobs and readout - incoming pushes must not snap them back mid-gesture.
  if (!sliderDragging)
  {
    char readout[32];
    if (!usable)
    {
      lv_label_set_text(setPointReadout, mode == RunMode::Off || mode == RunMode::Disabled ? "System off" : "No set point");
    }
    else if (range)
    {
      int32_t low = clampSliderValue(TemperatureFormat::celsiusToWholeFahrenheit(heating->targetTemperatureC));
      int32_t high = clampSliderValue(TemperatureFormat::celsiusToWholeFahrenheit(cooling->targetTemperatureC));
      lv_slider_set_left_value(setPointSlider, low, LV_ANIM_OFF);
      lv_slider_set_value(setPointSlider, high, LV_ANIM_OFF);
      formatReadout(readout, sizeof(readout), true, low, high);
      lv_label_set_text(setPointReadout, readout);
    }
    else
    {
      int32_t value = clampSliderValue(TemperatureFormat::celsiusToWholeFahrenheit(single->targetTemperatureC));
      lv_slider_set_value(setPointSlider, value, LV_ANIM_OFF);
      formatReadout(readout, sizeof(readout), false, value, value);
      lv_label_set_text(setPointReadout, readout);
    }
  }

  bool hasCurrent = controlState.hasEnvironmentTemperature || hasEnvironmentReading;
  if (hasCurrent)
  {
    float currentC = controlState.hasEnvironmentTemperature ? controlState.environmentTemperatureC : environmentTemperatureC;
    float currentF = currentC * 9.0f / 5.0f + 32.0f;
    float fraction = (currentF - SLIDER_MIN_F) / (float)(SLIDER_MAX_F - SLIDER_MIN_F);
    fraction = fraction < 0.0f ? 0.0f : (fraction > 1.0f ? 1.0f : fraction);
    const int sliderWidth = DISPLAY_HORIZONTAL_RESOLUTION - 2 * CONTENT_PADDING - 2 * KNOB_INSET;

    char text[24];
    snprintf(text, sizeof(text), "now %.1f\u00B0F", currentF);
    lv_label_set_text(currentMarkerLabel, text);
    lv_obj_set_x(currentMarker, KNOB_INSET + (int)(fraction * sliderWidth) - 1);
    lv_obj_update_layout(currentMarkerLabel);
    lv_obj_align_to(currentMarkerLabel, currentMarker, LV_ALIGN_OUT_TOP_MID, 0, -2);
    lv_obj_remove_flag(currentMarker, LV_OBJ_FLAG_HIDDEN);
    lv_obj_remove_flag(currentMarkerLabel, LV_OBJ_FLAG_HIDDEN);
  }
  else
  {
    lv_obj_add_flag(currentMarker, LV_OBJ_FLAG_HIDDEN);
    lv_obj_add_flag(currentMarkerLabel, LV_OBJ_FLAG_HIDDEN);
  }
}

float *Ui::findOrAddSetPoint(RunType type)
{
  for (size_t i = 0; i < thermostatState.setPointCount; i++)
  {
    if (thermostatState.setPoints[i].type == type)
    {
      return &thermostatState.setPoints[i].targetTemperatureC;
    }
  }
  if (thermostatState.setPointCount < MAX_SET_POINTS)
  {
    SetPointState &added = thermostatState.setPoints[thermostatState.setPointCount++];
    added.type = type;
    return &added.targetTemperatureC;
  }
  return nullptr;
}

void Ui::commitSliderSetPoints()
{
  auto commit = [this](RunType type, int32_t fahrenheit)
  {
    float celsius = TemperatureFormat::wholeFahrenheitToCelsius(fahrenheit);
    float *stored = findOrAddSetPoint(type);
    if (stored != nullptr)
    {
      *stored = celsius;
    }
    onSetPointAdjusted(type, celsius);
  };

  if (thermostatState.mode == RunMode::Automatic)
  {
    commit(RunType::Heating, lv_slider_get_left_value(setPointSlider));
    commit(RunType::Cooling, lv_slider_get_value(setPointSlider));
  }
  else if (thermostatState.mode == RunMode::Heating || thermostatState.mode == RunMode::Cooling)
  {
    commit(thermostatState.mode == RunMode::Cooling ? RunType::Cooling : RunType::Heating, lv_slider_get_value(setPointSlider));
  }

  refreshDial();
}

void Ui::applyBattery(int level, bool externalPower)
{
  if (level < 0)
  {
    lv_label_set_text(batteryLabel, "");
    return;
  }

  const char *symbol = level > 87   ? LV_SYMBOL_BATTERY_FULL
                       : level > 62 ? LV_SYMBOL_BATTERY_3
                       : level > 37 ? LV_SYMBOL_BATTERY_2
                       : level > 12 ? LV_SYMBOL_BATTERY_1
                                    : LV_SYMBOL_BATTERY_EMPTY;
  char text[32];
  snprintf(text, sizeof(text), "%s%s  %d%%", externalPower ? LV_SYMBOL_CHARGE "  " : "", symbol, level);
  lv_label_set_text(batteryLabel, text);
}

void Ui::refreshModeButtons()
{
  static const RunMode modes[MODE_BUTTON_COUNT] = {RunMode::Automatic, RunMode::Heating, RunMode::Cooling, RunMode::Off};

  for (size_t i = 0; i < MODE_BUTTON_COUNT; i++)
  {
    bool selected = thermostatState.mode == modes[i];
    lv_color_t color = UiTheme::accentIdle();
    if (selected)
    {
      switch (modes[i])
      {
      case RunMode::Automatic:
        color = UiTheme::accentSuccess();
        break;
      case RunMode::Heating:
        color = UiTheme::accentHeat();
        break;
      case RunMode::Cooling:
        color = UiTheme::accentCool();
        break;
      default:
        break;
      }
    }
    lv_obj_set_style_bg_color(modeButtons[i], color, 0);
    lv_obj_set_style_bg_opa(modeButtons[i], selected ? LV_OPA_COVER : LV_OPA_TRANSP, 0);
    lv_obj_set_style_text_color(lv_obj_get_child(modeButtons[i], 0), selected ? UiTheme::textMain() : UiTheme::textMuted(), 0);
    lv_obj_set_style_opa(modeButtons[i], thermostatState.mode == RunMode::Disabled ? LV_OPA_50 : LV_OPA_COVER, 0);
  }
}

void Ui::refreshFanButton()
{
  bool on = thermostatState.fanEnabled;
  lv_label_set_text(fanButtonLabel, on ? "Fan On" : "Fan Auto");
  lv_obj_set_style_bg_color(fanButton, on ? UiTheme::accentPrimary() : UiTheme::bgPanel(), 0);
  lv_obj_set_style_border_color(fanButton, on ? UiTheme::accentPrimary() : UiTheme::borderSubtle(), 0);
  lv_obj_set_style_text_color(fanButtonLabel, on ? UiTheme::textMain() : UiTheme::textMuted(), 0);
  lv_obj_set_style_opa(fanButton, thermostatState.mode == RunMode::Disabled ? LV_OPA_50 : LV_OPA_COVER, 0);
}

void Ui::refreshSensorCard()
{
  if (hasEnvironmentReading)
  {
    char buffer[32];
    formatFahrenheit(environmentTemperatureC, buffer, sizeof(buffer));
    lv_label_set_text(onboardLabel, buffer);
  }
  else
  {
    lv_label_set_text(onboardLabel, "--");
  }
}

void Ui::applyThermostatState(const uint8_t *state, size_t stateLength)
{
  ThermostatState parsed;
  if (!ThermostatStateParser::parse(state, stateLength, parsed))
  {
    LOG_PRINTLN("Failed to parse a thermostat state sync (truncated/malformed) - ignoring.");
    return;
  }

  thermostatState = parsed;
  refreshDial();
  refreshModeButtons();
  refreshFanButton();
  settings.applyState(thermostatState);
}

void Ui::applyControlState(const ControlCallState &state)
{
  controlState = state;
  refreshDial();
}

void Ui::applyEnvironmentAverage(const EnvironmentAverageReading &reading)
{
  // Only the environment/humidity/CO2 fields are refreshed here - the HVAC call state and target
  // temperature stay whatever the last ControlStateChanged push said, since this message doesn't carry them.
  controlState.hasEnvironmentTemperature = true;
  controlState.environmentTemperatureC = reading.temperatureC;
  controlState.hasHumidity = true;
  controlState.humidityPercentage = reading.humidityPercentage;
  controlState.hasCO2 = true;
  controlState.co2LevelPpm = reading.co2LevelPpm;
  refreshDial();
}

void Ui::applyEnvironmentReading(float temperatureC, float humidityPercentage)
{
  hasEnvironmentReading = true;
  environmentTemperatureC = temperatureC;
  environmentHumidityPercentage = humidityPercentage;
  refreshDial();
  refreshSensorCard();
}

void Ui::onModeSelected(RunMode mode)
{
  // Optimistic local update - the server will also broadcast an authoritative ThermostatStateChunk back,
  // but reflecting the tap immediately keeps the panel feeling responsive over the ESP-NOW round trip.
  thermostatState.mode = mode;
  refreshModeButtons();
  refreshFanButton();
  refreshDial();

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
  thermostatState.fanEnabled = enabled;
  refreshFanButton();

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

void Ui::onUpsertSchedule(const uint8_t *scheduleId, RunType type, uint16_t startMinutes, uint16_t endMinutes, float targetTemperatureC)
{
  if (communicator == nullptr)
  {
    return;
  }

  uint8_t buffer[Communicator::MAX_FRAME_CONTENT];
  size_t length =
      HmiCommandEncoder::encodeUpsertSchedule(scheduleId, type, startMinutes, endMinutes, targetTemperatureC, buffer, sizeof(buffer));
  if (length > 0)
  {
    communicator->send(buffer, length);
  }
}

void Ui::onRemoveSchedule(const uint8_t scheduleId[16])
{
  if (communicator == nullptr)
  {
    return;
  }

  uint8_t buffer[Communicator::MAX_FRAME_CONTENT];
  size_t length = HmiCommandEncoder::encodeRemoveSchedule(scheduleId, buffer, sizeof(buffer));
  if (length > 0)
  {
    communicator->send(buffer, length);
  }
}

void Ui::onSetForecastLockouts(const float *heatingLockoutC, const float *coolingLockoutC)
{
  thermostatState.hasHeatingLockout = heatingLockoutC != nullptr;
  thermostatState.heatingLockoutC = heatingLockoutC != nullptr ? *heatingLockoutC : 0.0f;
  thermostatState.hasCoolingLockout = coolingLockoutC != nullptr;
  thermostatState.coolingLockoutC = coolingLockoutC != nullptr ? *coolingLockoutC : 0.0f;

  if (communicator == nullptr)
  {
    return;
  }

  uint8_t buffer[Communicator::MAX_FRAME_CONTENT];
  size_t length = HmiCommandEncoder::encodeSetForecastLockouts(heatingLockoutC, coolingLockoutC, buffer, sizeof(buffer));
  if (length > 0)
  {
    communicator->send(buffer, length);
  }
}

void Ui::openSettings()
{
  settings.applyState(thermostatState);
  settings.reset();
  lv_screen_load(settings.screen());
}

void Ui::closeSettings()
{
  lv_screen_load(mainScreen);
}

void Ui::handleModeButtonClicked(lv_event_t *e)
{
  auto *binding = (ModeButtonBinding *)lv_event_get_user_data(e);
  binding->ui->onModeSelected(binding->mode);
}

void Ui::handleFanButtonClicked(lv_event_t *e)
{
  auto *ui = (Ui *)lv_event_get_user_data(e);
  ui->onFanToggled(!ui->thermostatState.fanEnabled);
}

void Ui::handleSettingsButtonClicked(lv_event_t *e)
{
  ((Ui *)lv_event_get_user_data(e))->openSettings();
}

void Ui::handleSliderEvent(lv_event_t *e)
{
  auto *ui = (Ui *)lv_event_get_user_data(e);
  lv_event_code_t code = lv_event_get_code(e);

  if (code == LV_EVENT_PRESSED)
  {
    ui->sliderDragging = true;
  }
  else if (code == LV_EVENT_VALUE_CHANGED)
  {
    char readout[32];
    bool range = ui->thermostatState.mode == RunMode::Automatic;
    formatReadout(readout, sizeof(readout), range, range ? lv_slider_get_left_value(ui->setPointSlider) : 0,
                  lv_slider_get_value(ui->setPointSlider));
    lv_label_set_text(ui->setPointReadout, readout);
  }
  else if (code == LV_EVENT_RELEASED || code == LV_EVENT_PRESS_LOST)
  {
    // Send once on release rather than on every step of the drag, to avoid flooding the ESP-NOW link.
    ui->sliderDragging = false;
    ui->commitSliderSetPoints();
  }
}
