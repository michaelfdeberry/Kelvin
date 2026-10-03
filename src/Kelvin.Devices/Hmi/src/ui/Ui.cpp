#include <stdio.h>
#include "Ui.h"
#include "UiTheme.h"
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
  lv_color_t drawBuffer[DISPLAY_HORIZONTAL_RESOLUTION * 10];

  // Must match ESP_PANEL_BOARD_TOUCH_INT_IO in esp_panel_board_custom_conf.h - briefly driven as an output
  // during the touch-reset dance below (mirrors the official ESP32-S3-Touch-LCD-7B demo's gt911.cpp), then
  // handed back to the library, which reconfigures it as an input for real interrupt use.
  const int TOUCH_INT_PIN = 4;

  void formatFahrenheit(float celsius, char *buffer, size_t bufferSize)
  {
    snprintf(buffer, bufferSize, "%.1f\u00B0F", celsius * 9.0f / 5.0f + 32.0f);
  }
}

void Ui::flushCallback(lv_display_t *display, const lv_area_t *area, uint8_t *pixelMap)
{
  if (uiInstance != nullptr && uiInstance->lcd != nullptr)
  {
    int width = area->x2 - area->x1 + 1;
    int height = area->y2 - area->y1 + 1;
    uiInstance->lcd->drawBitmap(area->x1, area->y1, width, height, pixelMap);
  }
  lv_display_flush_ready(display);
}

void Ui::touchReadCallback(lv_indev_t *indev, lv_indev_data_t *data)
{
  data->state = LV_INDEV_STATE_RELEASED;

  if (uiInstance == nullptr || uiInstance->touch == nullptr)
  {
    return;
  }

  esp_panel::drivers::TouchPoint point;
  if (uiInstance->touch->readPoints(&point, 1) > 0)
  {
    data->point.x = point.x;
    data->point.y = point.y;
    data->state = LV_INDEV_STATE_PRESSED;
  }
}

void Ui::begin(Communicator &communicatorRef)
{
  communicator = &communicatorRef;
  uiInstance = this;

  // Wire.begin() has already run in Hmi.ino's setup() by this point - safe to talk to the IO extension
  // chip now. Reset GT911 touch via the IO extension's TOUCH_RESET pin before bringing up the panel board,
  // matching the official 7B demo's gt911.cpp init dance (IO_EXTENSION_IO_1 low -> INT pin low -> IO_1
  // high), since this board's IO extension chip isn't one ESP32_Display_Panel's expander abstraction
  // supports (see esp_panel_board_custom_conf.h's USE_EXPANDER comment).
  ioExtension.begin(Wire);
  ioExtension.initialize();
  ioExtension.setOutput(IoExtensionPin::TOUCH_RESET, false);
  pinMode(TOUCH_INT_PIN, OUTPUT);
  delay(100);
  digitalWrite(TOUCH_INT_PIN, LOW);
  delay(100);
  ioExtension.setOutput(IoExtensionPin::TOUCH_RESET, true);
  delay(200);

  panelBoard = new esp_panel::board::Board();
  if (!panelBoard->begin())
  {
    LOG_PRINTLN("Failed to initialize the display panel board - the RGB bus/touch may be unresponsive.");
  }
  lcd = panelBoard->getLCD();
  touch = panelBoard->getTouch();

  // Backlight is switched on directly (not through the library - see esp_panel_board_custom_conf.h's
  // USE_BACKLIGHT comment) only after the panel is up, to avoid a brief flash of garbage pixels.
  ioExtension.setOutput(IoExtensionPin::BACKLIGHT, true);

  lv_init();

  lv_display_t *display = lv_display_create(DISPLAY_HORIZONTAL_RESOLUTION, DISPLAY_VERTICAL_RESOLUTION);
  lv_display_set_flush_cb(display, flushCallback);
  lv_display_set_buffers(display, drawBuffer, nullptr, sizeof(drawBuffer), LV_DISPLAY_RENDER_MODE_PARTIAL);

  lv_indev_t *touchIndev = lv_indev_create();
  lv_indev_set_type(touchIndev, LV_INDEV_TYPE_POINTER);
  lv_indev_set_read_cb(touchIndev, touchReadCallback);

  lv_obj_t *screen = lv_screen_active();
  lv_obj_set_style_bg_color(screen, UiTheme::bgDark(), 0);
  lv_obj_set_style_bg_opa(screen, LV_OPA_COVER, 0);
  lv_obj_set_style_pad_all(screen, 16, 0);
  lv_obj_set_flex_flow(screen, LV_FLEX_FLOW_COLUMN);
  lv_obj_set_flex_align(screen, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER);
  lv_obj_set_style_pad_gap(screen, 14, 0);

  buildDial(screen);
  buildControls(screen);
  buildSensorCard(screen);

  editor.begin(screen, *this);

  refreshDial();
  refreshModeButtons();
  refreshFanButton();
  refreshSensorCard();

  LOG_PRINTLN("LVGL initialized, display/touch driven via ESP32_Display_Panel's custom ESP32-S3-Touch-LCD-7B board config.");
}

void Ui::tick()
{
  lv_timer_handler();
}

void Ui::buildDial(lv_obj_t *parent)
{
  lv_obj_t *dialContainer = lv_obj_create(parent);
  lv_obj_remove_style_all(dialContainer);
  lv_obj_set_size(dialContainer, 260, 260);

  dialArc = lv_arc_create(dialContainer);
  lv_obj_set_size(dialArc, 260, 260);
  lv_obj_center(dialArc);
  lv_arc_set_bg_angles(dialArc, 0, 360);
  lv_arc_set_range(dialArc, 0, 100);
  lv_arc_set_value(dialArc, 60); // decorative - this panel has no draggable set point on the ring itself
  lv_obj_remove_flag(dialArc, LV_OBJ_FLAG_CLICKABLE);
  lv_obj_set_style_arc_width(dialArc, 16, LV_PART_MAIN);
  lv_obj_set_style_arc_color(dialArc, UiTheme::bgPanel(), LV_PART_MAIN);
  lv_obj_set_style_arc_width(dialArc, 16, LV_PART_INDICATOR);
  lv_obj_set_style_pad_all(dialArc, 0, LV_PART_KNOB);

  lv_obj_t *inner = lv_obj_create(dialContainer);
  lv_obj_set_size(inner, 210, 210);
  lv_obj_center(inner);
  lv_obj_set_style_radius(inner, LV_RADIUS_CIRCLE, 0);
  lv_obj_set_style_bg_color(inner, UiTheme::bgDark(), 0);
  lv_obj_set_style_border_width(inner, 0, 0);
  lv_obj_set_flex_flow(inner, LV_FLEX_FLOW_COLUMN);
  lv_obj_set_flex_align(inner, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER);
  lv_obj_set_style_pad_gap(inner, 4, 0);

  targetTempLabel = lv_label_create(inner);
  lv_obj_set_style_text_font(targetTempLabel, UiTheme::fontBody(), 0);

  currentTempLabel = lv_label_create(inner);
  lv_obj_set_style_text_font(currentTempLabel, UiTheme::fontHero(), 0);
  lv_obj_set_style_text_color(currentTempLabel, UiTheme::textMain(), 0);

  environmentSubtitleLabel = lv_label_create(inner);
  lv_obj_set_style_text_font(environmentSubtitleLabel, UiTheme::fontBody(), 0);
  lv_obj_set_style_text_color(environmentSubtitleLabel, UiTheme::textMuted(), 0);

  statusBadge = lv_label_create(inner);
  lv_obj_set_style_text_font(statusBadge, UiTheme::fontBody(), 0);
  lv_obj_set_style_pad_hor(statusBadge, 12, 0);
  lv_obj_set_style_pad_ver(statusBadge, 4, 0);
  lv_obj_set_style_radius(statusBadge, 20, 0);
  lv_obj_set_style_border_width(statusBadge, 1, 0);

  editButton = lv_button_create(inner);
  lv_obj_set_size(editButton, 72, 36);
  lv_obj_set_style_bg_color(editButton, UiTheme::bgPanel(), 0);
  lv_obj_t *editLabel = lv_label_create(editButton);
  lv_label_set_text(editLabel, "Edit");
  lv_obj_set_style_text_color(editLabel, UiTheme::textMain(), 0);
  lv_obj_center(editLabel);
  lv_obj_add_event_cb(editButton, handleEditButtonClicked, LV_EVENT_CLICKED, this);
}

void Ui::buildControls(lv_obj_t *parent)
{
  lv_obj_t *modeRow = lv_obj_create(parent);
  lv_obj_remove_style_all(modeRow);
  lv_obj_set_size(modeRow, LV_SIZE_CONTENT, LV_SIZE_CONTENT);
  lv_obj_set_flex_flow(modeRow, LV_FLEX_FLOW_ROW);
  lv_obj_set_flex_align(modeRow, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER);
  lv_obj_set_style_pad_gap(modeRow, 10, 0);

  static const char *labels[MODE_BUTTON_COUNT] = {"Auto", "Heat", "Cool", "Off"};
  static const RunMode modes[MODE_BUTTON_COUNT] = {RunMode::Automatic, RunMode::Heating, RunMode::Cooling, RunMode::Off};

  for (size_t i = 0; i < MODE_BUTTON_COUNT; i++)
  {
    lv_obj_t *button = lv_button_create(modeRow);
    lv_obj_set_size(button, 110, 48);
    lv_obj_t *label = lv_label_create(button);
    lv_label_set_text(label, labels[i]);
    lv_obj_set_style_text_font(label, UiTheme::fontLabel(), 0);
    lv_obj_set_style_text_color(label, UiTheme::textMain(), 0);
    lv_obj_center(label);

    modeButtons[i] = button;
    modeButtonBindings[i] = {this, modes[i]};
    lv_obj_add_event_cb(button, handleModeButtonClicked, LV_EVENT_CLICKED, &modeButtonBindings[i]);
  }

  fanButton = lv_button_create(parent);
  lv_obj_set_size(fanButton, 150, 44);
  fanButtonLabel = lv_label_create(fanButton);
  lv_obj_set_style_text_font(fanButtonLabel, UiTheme::fontBody(), 0);
  lv_obj_set_style_text_color(fanButtonLabel, UiTheme::textMain(), 0);
  lv_obj_center(fanButtonLabel);
  lv_obj_add_event_cb(fanButton, handleFanButtonClicked, LV_EVENT_CLICKED, this);
}

void Ui::buildSensorCard(lv_obj_t *parent)
{
  lv_obj_t *card = lv_obj_create(parent);
  lv_obj_set_size(card, 300, 100);
  lv_obj_set_style_bg_color(card, UiTheme::bgPanel(), 0);
  lv_obj_set_style_border_color(card, UiTheme::borderSubtle(), 0);
  lv_obj_set_style_border_width(card, 1, 0);
  lv_obj_set_style_radius(card, 10, 0);
  lv_obj_set_flex_flow(card, LV_FLEX_FLOW_COLUMN);
  lv_obj_set_flex_align(card, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER);
  lv_obj_set_style_pad_gap(card, 2, 0);

  lv_obj_t *title = lv_label_create(card);
  lv_label_set_text(title, "Onboard Sensor");
  lv_obj_set_style_text_color(title, UiTheme::textMuted(), 0);
  lv_obj_set_style_text_font(title, UiTheme::fontBody(), 0);

  sensorValueLabel = lv_label_create(card);
  lv_obj_set_style_text_color(sensorValueLabel, UiTheme::textMain(), 0);
  lv_obj_set_style_text_font(sensorValueLabel, UiTheme::fontValue(), 0);

  sensorSubtitleLabel = lv_label_create(card);
  lv_obj_set_style_text_color(sensorSubtitleLabel, UiTheme::textMuted(), 0);
  lv_obj_set_style_text_font(sensorSubtitleLabel, UiTheme::fontBody(), 0);
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

  lv_obj_set_style_arc_color(dialArc, accent, LV_PART_INDICATOR);
  lv_obj_set_style_text_color(targetTempLabel, accent, 0);

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

  if (controlState.hasHumidity && controlState.hasCO2)
  {
    snprintf(buffer, sizeof(buffer), "%.0f%% RH \u2022 %.0fppm CO2", controlState.humidityPercentage, controlState.co2LevelPpm);
    lv_label_set_text(environmentSubtitleLabel, buffer);
    lv_obj_remove_flag(environmentSubtitleLabel, LV_OBJ_FLAG_HIDDEN);
  }
  else if (controlState.hasHumidity)
  {
    snprintf(buffer, sizeof(buffer), "%.0f%% RH", controlState.humidityPercentage);
    lv_label_set_text(environmentSubtitleLabel, buffer);
    lv_obj_remove_flag(environmentSubtitleLabel, LV_OBJ_FLAG_HIDDEN);
  }
  else if (controlState.hasCO2)
  {
    snprintf(buffer, sizeof(buffer), "%.0fppm CO2", controlState.co2LevelPpm);
    lv_label_set_text(environmentSubtitleLabel, buffer);
    lv_obj_remove_flag(environmentSubtitleLabel, LV_OBJ_FLAG_HIDDEN);
  }
  else
  {
    lv_obj_add_flag(environmentSubtitleLabel, LV_OBJ_FLAG_HIDDEN);
  }

  bool hasTarget = false;
  float targetTemperatureC = 0.0f;
  if (thermostatState.mode != RunMode::Disabled && thermostatState.mode != RunMode::Off)
  {
    if (controlState.hasTargetTemperature)
    {
      targetTemperatureC = controlState.targetTemperatureC;
      hasTarget = true;
    }
    else
    {
      RunType activeType = thermostatState.mode == RunMode::Cooling ? RunType::Cooling : RunType::Heating;
      const SetPointState *setPoint = thermostatState.findSetPoint(activeType);
      if (setPoint != nullptr)
      {
        targetTemperatureC = setPoint->targetTemperatureC;
        hasTarget = true;
      }
    }
  }

  if (hasTarget)
  {
    char targetBuffer[16];
    formatFahrenheit(targetTemperatureC, targetBuffer, sizeof(targetBuffer));
    snprintf(buffer, sizeof(buffer), "Set to %s", targetBuffer);
    lv_label_set_text(targetTempLabel, buffer);
  }
  else
  {
    lv_label_set_text(targetTempLabel, "");
  }
}

void Ui::refreshModeButtons()
{
  static const RunMode modes[MODE_BUTTON_COUNT] = {RunMode::Automatic, RunMode::Heating, RunMode::Cooling, RunMode::Off};

  for (size_t i = 0; i < MODE_BUTTON_COUNT; i++)
  {
    lv_color_t color = UiTheme::bgPanel();
    if (thermostatState.mode == modes[i])
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
        color = UiTheme::accentIdle();
        break;
      }
    }
    lv_obj_set_style_bg_color(modeButtons[i], color, 0);
    lv_obj_set_style_opa(modeButtons[i], thermostatState.mode == RunMode::Disabled ? LV_OPA_50 : LV_OPA_COVER, 0);
  }
}

void Ui::refreshFanButton()
{
  lv_label_set_text(fanButtonLabel, thermostatState.fanEnabled ? "Fan: On" : "Fan: Auto");
  lv_obj_set_style_bg_color(fanButton, thermostatState.fanEnabled ? UiTheme::accentPrimary() : UiTheme::bgPanel(), 0);
  lv_obj_set_style_opa(fanButton, thermostatState.mode == RunMode::Disabled ? LV_OPA_50 : LV_OPA_COVER, 0);
}

void Ui::refreshSensorCard()
{
  if (hasEnvironmentReading)
  {
    char buffer[32];
    formatFahrenheit(environmentTemperatureC, buffer, sizeof(buffer));
    lv_label_set_text(sensorValueLabel, buffer);

    char subtitle[24];
    snprintf(subtitle, sizeof(subtitle), "%.1f%% RH", environmentHumidityPercentage);
    lv_label_set_text(sensorSubtitleLabel, subtitle);
  }
  else
  {
    lv_label_set_text(sensorValueLabel, "--");
    lv_label_set_text(sensorSubtitleLabel, "--");
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

void Ui::handleEditButtonClicked(lv_event_t *e)
{
  auto *ui = (Ui *)lv_event_get_user_data(e);
  ui->editor.open(ui->thermostatState);
}
