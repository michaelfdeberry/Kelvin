#pragma once

#include <lvgl.h>
#include <HmiProtocol.h>
#include "../protocol/ThermostatStateParser.h"

class Ui;

// Full-screen settings page (its own LVGL screen, opened by the gear button on the main screen) with two
// tabs: read/edit heating + cooling schedules (a 24h timeline plus a tappable list per mode, edited in a small
// sheet with time rollers) and the forecast lockouts. Changes are sent to the server immediately; the server
// answers with a fresh ThermostatStateChunk, which is what actually updates the schedule lists (applyState).
class SettingsPage
{
public:
  void begin(Ui &uiRef);
  lv_obj_t *screen() const { return page; }

  // Repopulates every list/control from the latest known thermostat state.
  void applyState(const ThermostatState &state);

  // Resets to the Schedules tab with the edit sheet closed - call before showing the page.
  void reset();

private:
  struct IndexBinding
  {
    SettingsPage *page;
    size_t index;
  };

  Ui *ui = nullptr;
  ThermostatState latest;

  lv_obj_t *page = nullptr;

  lv_obj_t *tabButtons[2] = {nullptr, nullptr};
  lv_obj_t *scheduleTab = nullptr;
  lv_obj_t *lockoutTab = nullptr;

  // Index 0 = heating, 1 = cooling.
  lv_obj_t *timelines[2] = {nullptr, nullptr};
  lv_obj_t *lists[2] = {nullptr, nullptr};

  // Schedule edit sheet
  lv_obj_t *sheet = nullptr;
  lv_obj_t *sheetTitle = nullptr;
  lv_obj_t *startRollers[3] = {nullptr, nullptr, nullptr};
  lv_obj_t *endRollers[3] = {nullptr, nullptr, nullptr};
  lv_obj_t *targetSlider = nullptr;
  lv_obj_t *targetLabel = nullptr;
  lv_obj_t *sheetError = nullptr;
  lv_obj_t *deleteButton = nullptr;
  bool editHasId = false;
  uint8_t editId[16] = {0};
  RunType editType = RunType::Heating;

  // Forecast lockouts (index 0 = heating, 1 = cooling)
  lv_obj_t *lockoutSwitches[2] = {nullptr, nullptr};
  lv_obj_t *lockoutSliders[2] = {nullptr, nullptr};
  lv_obj_t *lockoutValueLabels[2] = {nullptr, nullptr};
  bool lockoutEnabled[2] = {false, false};
  int32_t lockoutF[2] = {70, 55};
  bool lockoutDragging = false;

  IndexBinding tabBindings[2];
  IndexBinding columnBindings[2];
  IndexBinding lockoutBindings[2];
  IndexBinding rowBindings[MAX_SCHEDULES];

  void buildHeader(lv_obj_t *parent);
  void buildTabs(lv_obj_t *parent);
  void buildScheduleTab(lv_obj_t *parent);
  void buildScheduleColumn(lv_obj_t *parent, size_t index);
  void buildLockoutTab(lv_obj_t *parent);
  void buildLockoutCard(lv_obj_t *parent, size_t index);
  void buildSheet(lv_obj_t *parent);
  lv_obj_t *buildTimeRollers(lv_obj_t *parent, const char *caption, lv_obj_t *rollers[3]);

  void showTab(size_t index);
  void rebuildSchedules();
  void addScheduleRow(size_t column, size_t scheduleIndex);
  void refreshLockouts();
  void refreshLockoutDimming(size_t index);
  void sendLockouts();

  void openSheet(RunType type, const ScheduleState *existing);
  void closeSheet();
  void saveSheet();
  void deleteFromSheet();

  static void handleBackClicked(lv_event_t *e);
  static void handleTabClicked(lv_event_t *e);
  static void handleAddClicked(lv_event_t *e);
  static void handleRowClicked(lv_event_t *e);
  static void handleSheetSave(lv_event_t *e);
  static void handleSheetCancel(lv_event_t *e);
  static void handleSheetDelete(lv_event_t *e);
  static void handleTargetChanged(lv_event_t *e);
  static void handleLockoutSwitch(lv_event_t *e);
  static void handleLockoutSlider(lv_event_t *e);
};
