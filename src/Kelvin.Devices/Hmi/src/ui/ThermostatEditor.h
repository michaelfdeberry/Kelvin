#pragma once

#include <lvgl.h>
#include <HmiProtocol.h>
#include "../protocol/ThermostatStateParser.h"

class Ui;

// The "Set Points & Schedules" edit modal (pencil icon on the main screen), matching Kelvin.Client's
// thermostat-editor tabs minus the Forecast tab (weather isn't supported on this panel). Owns its own draft
// copy of set points/schedules so Cancel can discard in-progress edits; Save sends one SetSetPoint message
// per set point and one UpsertSchedule/RemoveSchedule message per changed/removed schedule via `Ui`.
class ThermostatEditor
{
public:
  void begin(lv_obj_t *parent, Ui &uiRef);

  // Repopulates every field from the latest known thermostat state and shows the modal.
  void open(const ThermostatState &state);

  // A pair of -/+ buttons plus the label they update. `target` points at either a member of this class (the
  // set point rows) or a heap-allocated schedule row's own storage, so these only ever need to outlive the
  // buttons they're bound to. Public so the schedule row bookkeeping in ThermostatEditor.cpp (an ordinary
  // file-local struct, not a member of this class) can hold an array of them.
  struct StepperBinding
  {
    int32_t *target;
    int32_t step;
    int32_t minValue;
    int32_t maxValue;
    int32_t direction; // +1 or -1, baked in per button
    lv_obj_t *valueLabel;
    void (*format)(int32_t value, char *buffer, size_t bufferSize);
  };

private:
  // A row's in-progress edits, converted to/from ScheduleState only at open()/handleSave().
  struct ScheduleDraft
  {
    bool hasId = false;
    uint8_t id[16] = {0};
    int32_t startMinutes = 0;
    int32_t endMinutes = 0;
    int32_t targetTemperatureF = 70;
  };

  Ui *ui = nullptr;

  lv_obj_t *overlay = nullptr;
  lv_obj_t *tabview = nullptr;

  lv_obj_t *heatingRow = nullptr;
  lv_obj_t *coolingRow = nullptr;
  lv_obj_t *heatingValueLabel = nullptr;
  lv_obj_t *coolingValueLabel = nullptr;
  int32_t heatingSetPointF = 68;
  int32_t coolingSetPointF = 75;
  StepperBinding heatingSetPointBindings[2];
  StepperBinding coolingSetPointBindings[2];

  lv_obj_t *heatScheduleList = nullptr;
  lv_obj_t *coolScheduleList = nullptr;
  ScheduleDraft heatSchedules[MAX_SCHEDULES];
  size_t heatScheduleCount = 0;
  ScheduleDraft coolSchedules[MAX_SCHEDULES];
  size_t coolScheduleCount = 0;

  // Ids removed this editing session (existing schedules the user tapped Remove on) - sent as
  // RemoveSchedule on Save. New, never-saved rows (hasId == false) are just dropped from the draft array.
  uint8_t removedScheduleIds[MAX_SCHEDULES][16];
  size_t removedScheduleCount = 0;

  void buildOverlay(lv_obj_t *parent);
  lv_obj_t *buildModeTab(const char *name);
  void buildSetPointsTab(lv_obj_t *tab);
  lv_obj_t *buildStepperRow(
      lv_obj_t *parent,
      const char *labelText,
      int32_t *value,
      int32_t step,
      int32_t minValue,
      int32_t maxValue,
      StepperBinding bindings[2],
      void (*format)(int32_t value, char *buffer, size_t bufferSize));
  void buildScheduleTab(lv_obj_t *tab, RunType type, lv_obj_t **outList);

  ScheduleDraft *draftsFor(RunType type);
  size_t *draftCountFor(RunType type);
  void rebuildScheduleList(RunType type);
  void addSchedule(RunType type);
  void removeSchedule(RunType type, size_t index);

  void handleSave();
  void handleCancel();
  void close();

  static void handleStepperClicked(lv_event_t *e);
  static void handleSaveClicked(lv_event_t *e);
  static void handleCancelClicked(lv_event_t *e);
  static void handleAddHeatScheduleClicked(lv_event_t *e);
  static void handleAddCoolScheduleClicked(lv_event_t *e);
  static void handleRemoveScheduleClicked(lv_event_t *e);
  static void freeRowContext(lv_event_t *e);
};
