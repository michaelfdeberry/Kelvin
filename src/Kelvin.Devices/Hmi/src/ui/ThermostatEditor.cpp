#include <stdio.h>
#include <string.h>
#include "ThermostatEditor.h"
#include "Ui.h"
#include "UiTheme.h"
#include "TemperatureFormat.h"

namespace
{
  void formatTemperatureF(int32_t value, char *buffer, size_t bufferSize)
  {
    snprintf(buffer, bufferSize, "%ld\u00B0F", (long)value);
  }

  void formatTime(int32_t minutesOfDay, char *buffer, size_t bufferSize)
  {
    int32_t hours = (minutesOfDay / 60) % 24;
    int32_t minutes = minutesOfDay % 60;
    snprintf(buffer, bufferSize, "%02ld:%02ld", (long)hours, (long)minutes);
  }

  // Heap-allocated per schedule row so the row count can grow/shrink freely - freed via the row's
  // LV_EVENT_DELETE (fired by lv_obj_clean() when the list is rebuilt), see ThermostatEditor::freeRowContext.
  struct RowContext
  {
    ThermostatEditor::StepperBinding steppers[6];
    ThermostatEditor *editor;
    RunType type;
    size_t index;
  };
}

void ThermostatEditor::begin(lv_obj_t *parent, Ui &uiRef)
{
  ui = &uiRef;
  buildOverlay(parent);
}

void ThermostatEditor::buildOverlay(lv_obj_t *parent)
{
  overlay = lv_obj_create(parent);
  lv_obj_remove_style_all(overlay);
  lv_obj_set_size(overlay, LV_PCT(100), LV_PCT(100));
  lv_obj_set_pos(overlay, 0, 0);
  lv_obj_add_flag(overlay, LV_OBJ_FLAG_IGNORE_LAYOUT);
  lv_obj_set_style_bg_color(overlay, lv_color_black(), 0);
  lv_obj_set_style_bg_opa(overlay, LV_OPA_60, 0);
  lv_obj_set_flex_flow(overlay, LV_FLEX_FLOW_ROW);
  lv_obj_set_flex_align(overlay, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER);
  lv_obj_add_flag(overlay, LV_OBJ_FLAG_HIDDEN);

  lv_obj_t *modal = lv_obj_create(overlay);
  lv_obj_set_size(modal, 760, 480);
  lv_obj_set_style_bg_color(modal, UiTheme::bgPanel(), 0);
  lv_obj_set_style_border_color(modal, UiTheme::borderSubtle(), 0);
  lv_obj_set_style_border_width(modal, 1, 0);
  lv_obj_set_style_radius(modal, 12, 0);
  lv_obj_set_style_pad_all(modal, 20, 0);
  lv_obj_set_style_pad_gap(modal, 12, 0);
  lv_obj_set_flex_flow(modal, LV_FLEX_FLOW_COLUMN);

  lv_obj_t *header = lv_obj_create(modal);
  lv_obj_remove_style_all(header);
  lv_obj_set_size(header, LV_PCT(100), LV_SIZE_CONTENT);
  lv_obj_set_flex_flow(header, LV_FLEX_FLOW_ROW);
  lv_obj_set_flex_align(header, LV_FLEX_ALIGN_SPACE_BETWEEN, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER);

  lv_obj_t *title = lv_label_create(header);
  lv_label_set_text(title, "Set Points & Schedules");
  lv_obj_set_style_text_color(title, UiTheme::textMain(), 0);
  lv_obj_set_style_text_font(title, UiTheme::fontLabel(), 0);

  lv_obj_t *closeButton = lv_button_create(header);
  lv_obj_set_size(closeButton, 40, 40);
  lv_obj_set_style_bg_color(closeButton, UiTheme::bgDark(), 0);
  lv_obj_t *closeLabel = lv_label_create(closeButton);
  lv_label_set_text(closeLabel, "X");
  lv_obj_set_style_text_color(closeLabel, UiTheme::textMain(), 0);
  lv_obj_center(closeLabel);
  lv_obj_add_event_cb(closeButton, handleCancelClicked, LV_EVENT_CLICKED, this);

  tabview = lv_tabview_create(modal);
  lv_obj_set_size(tabview, LV_PCT(100), LV_PCT(100));
  lv_obj_set_flex_grow(tabview, 1);
  lv_obj_set_style_bg_color(tabview, UiTheme::bgPanel(), 0);

  buildSetPointsTab(lv_tabview_add_tab(tabview, "Set Points"));
  buildScheduleTab(lv_tabview_add_tab(tabview, "Heat Schedules"), RunType::Heating, &heatScheduleList);
  buildScheduleTab(lv_tabview_add_tab(tabview, "Cool Schedules"), RunType::Cooling, &coolScheduleList);

  lv_obj_t *footer = lv_obj_create(modal);
  lv_obj_remove_style_all(footer);
  lv_obj_set_size(footer, LV_PCT(100), LV_SIZE_CONTENT);
  lv_obj_set_flex_flow(footer, LV_FLEX_FLOW_ROW);
  lv_obj_set_flex_align(footer, LV_FLEX_ALIGN_END, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER);
  lv_obj_set_style_pad_gap(footer, 12, 0);

  lv_obj_t *cancelButton = lv_button_create(footer);
  lv_obj_set_size(cancelButton, 120, 48);
  lv_obj_set_style_bg_color(cancelButton, UiTheme::bgDark(), 0);
  lv_obj_set_style_border_color(cancelButton, UiTheme::borderSubtle(), 0);
  lv_obj_set_style_border_width(cancelButton, 1, 0);
  lv_obj_t *cancelLabel = lv_label_create(cancelButton);
  lv_label_set_text(cancelLabel, "Cancel");
  lv_obj_set_style_text_color(cancelLabel, UiTheme::textMain(), 0);
  lv_obj_center(cancelLabel);
  lv_obj_add_event_cb(cancelButton, handleCancelClicked, LV_EVENT_CLICKED, this);

  lv_obj_t *saveButton = lv_button_create(footer);
  lv_obj_set_size(saveButton, 120, 48);
  lv_obj_set_style_bg_color(saveButton, UiTheme::accentPrimary(), 0);
  lv_obj_t *saveLabel = lv_label_create(saveButton);
  lv_label_set_text(saveLabel, "Save");
  lv_obj_set_style_text_color(saveLabel, UiTheme::textOnPrimaryStrong(), 0);
  lv_obj_center(saveLabel);
  lv_obj_add_event_cb(saveButton, handleSaveClicked, LV_EVENT_CLICKED, this);
}

void ThermostatEditor::buildSetPointsTab(lv_obj_t *tab)
{
  lv_obj_set_flex_flow(tab, LV_FLEX_FLOW_COLUMN);
  lv_obj_set_style_pad_gap(tab, 10, 0);

  lv_obj_t *description = lv_label_create(tab);
  lv_label_set_text(description, "Set Points are used when no schedule is active.");
  lv_obj_set_style_text_color(description, UiTheme::textMuted(), 0);
  lv_obj_set_style_text_font(description, UiTheme::fontBody(), 0);

  heatingRow = buildStepperRow(tab, "Heating Set Point", &heatingSetPointF, 1, 40, 90, heatingSetPointBindings, formatTemperatureF);
  heatingValueLabel = heatingSetPointBindings[0].valueLabel;

  coolingRow = buildStepperRow(tab, "Cooling Set Point", &coolingSetPointF, 1, 40, 90, coolingSetPointBindings, formatTemperatureF);
  coolingValueLabel = coolingSetPointBindings[0].valueLabel;
}

lv_obj_t *ThermostatEditor::buildStepperRow(
    lv_obj_t *parent,
    const char *labelText,
    int32_t *value,
    int32_t step,
    int32_t minValue,
    int32_t maxValue,
    StepperBinding bindings[2],
    void (*format)(int32_t value, char *buffer, size_t bufferSize))
{
  lv_obj_t *row = lv_obj_create(parent);
  lv_obj_remove_style_all(row);
  lv_obj_set_size(row, LV_PCT(100), LV_SIZE_CONTENT);
  lv_obj_set_flex_flow(row, LV_FLEX_FLOW_ROW);
  lv_obj_set_flex_align(row, LV_FLEX_ALIGN_SPACE_BETWEEN, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER);

  lv_obj_t *label = lv_label_create(row);
  lv_label_set_text(label, labelText);
  lv_obj_set_style_text_color(label, UiTheme::textMain(), 0);
  lv_obj_set_style_text_font(label, UiTheme::fontLabel(), 0);

  lv_obj_t *stepper = lv_obj_create(row);
  lv_obj_set_size(stepper, 260, 56);
  lv_obj_set_style_bg_color(stepper, UiTheme::bgDark(), 0);
  lv_obj_set_style_border_color(stepper, UiTheme::borderSubtle(), 0);
  lv_obj_set_style_border_width(stepper, 1, 0);
  lv_obj_set_style_radius(stepper, 28, 0);
  lv_obj_set_style_pad_all(stepper, 4, 0);
  lv_obj_set_flex_flow(stepper, LV_FLEX_FLOW_ROW);
  lv_obj_set_flex_align(stepper, LV_FLEX_ALIGN_SPACE_BETWEEN, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER);

  lv_obj_t *minusButton = lv_button_create(stepper);
  lv_obj_set_size(minusButton, 48, 48);
  lv_obj_set_style_bg_color(minusButton, UiTheme::bgPanel(), 0);
  lv_obj_t *minusLabel = lv_label_create(minusButton);
  lv_label_set_text(minusLabel, "-");
  lv_obj_set_style_text_color(minusLabel, UiTheme::textMain(), 0);
  lv_obj_center(minusLabel);

  lv_obj_t *valueLabel = lv_label_create(stepper);
  lv_obj_set_flex_grow(valueLabel, 1);
  lv_obj_set_style_text_align(valueLabel, LV_TEXT_ALIGN_CENTER, 0);
  lv_obj_set_style_text_color(valueLabel, UiTheme::textMain(), 0);
  lv_obj_set_style_text_font(valueLabel, UiTheme::fontValue(), 0);
  char buffer[24];
  format(*value, buffer, sizeof(buffer));
  lv_label_set_text(valueLabel, buffer);

  lv_obj_t *plusButton = lv_button_create(stepper);
  lv_obj_set_size(plusButton, 48, 48);
  lv_obj_set_style_bg_color(plusButton, UiTheme::bgPanel(), 0);
  lv_obj_t *plusLabel = lv_label_create(plusButton);
  lv_label_set_text(plusLabel, "+");
  lv_obj_set_style_text_color(plusLabel, UiTheme::textMain(), 0);
  lv_obj_center(plusLabel);

  bindings[0] = {value, step, minValue, maxValue, -1, valueLabel, format};
  bindings[1] = {value, step, minValue, maxValue, 1, valueLabel, format};
  lv_obj_add_event_cb(minusButton, handleStepperClicked, LV_EVENT_CLICKED, &bindings[0]);
  lv_obj_add_event_cb(plusButton, handleStepperClicked, LV_EVENT_CLICKED, &bindings[1]);

  return row;
}

void ThermostatEditor::buildScheduleTab(lv_obj_t *tab, RunType type, lv_obj_t **outList)
{
  lv_obj_set_flex_flow(tab, LV_FLEX_FLOW_COLUMN);
  lv_obj_set_style_pad_gap(tab, 10, 0);

  lv_obj_t *description = lv_label_create(tab);
  lv_label_set_text(description, type == RunType::Heating ? "Create Heating schedules for the thermostat." : "Create Cooling schedules for the thermostat.");
  lv_obj_set_style_text_color(description, UiTheme::textMuted(), 0);
  lv_obj_set_style_text_font(description, UiTheme::fontBody(), 0);

  lv_obj_t *list = lv_obj_create(tab);
  lv_obj_set_size(list, LV_PCT(100), LV_SIZE_CONTENT);
  lv_obj_set_style_bg_opa(list, LV_OPA_TRANSP, 0);
  lv_obj_set_style_border_width(list, 0, 0);
  lv_obj_set_flex_flow(list, LV_FLEX_FLOW_COLUMN);
  lv_obj_set_style_pad_gap(list, 10, 0);
  *outList = list;

  lv_obj_t *addButton = lv_button_create(tab);
  lv_obj_set_style_bg_color(addButton, UiTheme::bgDark(), 0);
  lv_obj_set_style_border_color(addButton, UiTheme::borderSubtle(), 0);
  lv_obj_set_style_border_width(addButton, 1, 0);
  lv_obj_t *addLabel = lv_label_create(addButton);
  lv_label_set_text(addLabel, "+ Add Schedule Block");
  lv_obj_set_style_text_color(addLabel, UiTheme::textMain(), 0);
  lv_obj_center(addLabel);
  lv_obj_add_event_cb(addButton, type == RunType::Heating ? handleAddHeatScheduleClicked : handleAddCoolScheduleClicked, LV_EVENT_CLICKED, this);
}

ThermostatEditor::ScheduleDraft *ThermostatEditor::draftsFor(RunType type)
{
  return type == RunType::Heating ? heatSchedules : coolSchedules;
}

size_t *ThermostatEditor::draftCountFor(RunType type)
{
  return type == RunType::Heating ? &heatScheduleCount : &coolScheduleCount;
}

void ThermostatEditor::rebuildScheduleList(RunType type)
{
  lv_obj_t *list = type == RunType::Heating ? heatScheduleList : coolScheduleList;
  ScheduleDraft *drafts = draftsFor(type);
  size_t count = *draftCountFor(type);

  lv_obj_clean(list); // frees every row's RowContext via its LV_EVENT_DELETE handler

  for (size_t i = 0; i < count; i++)
  {
    RowContext *ctx = new RowContext();
    ctx->editor = this;
    ctx->type = type;
    ctx->index = i;

    lv_obj_t *row = lv_obj_create(list);
    lv_obj_set_size(row, LV_PCT(100), LV_SIZE_CONTENT);
    lv_obj_set_style_bg_color(row, UiTheme::bgDark(), 0);
    lv_obj_set_style_border_color(row, UiTheme::borderSubtle(), 0);
    lv_obj_set_style_border_width(row, 1, 0);
    lv_obj_set_style_radius(row, 10, 0);
    lv_obj_set_style_pad_all(row, 10, 0);
    lv_obj_set_flex_flow(row, LV_FLEX_FLOW_ROW);
    lv_obj_set_flex_align(row, LV_FLEX_ALIGN_SPACE_BETWEEN, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER);
    lv_obj_add_event_cb(row, freeRowContext, LV_EVENT_DELETE, ctx);

    struct MiniStepper
    {
      const char *label;
      int32_t *value;
      int32_t step, minValue, maxValue;
      void (*format)(int32_t, char *, size_t);
      size_t bindingOffset;
    };
    MiniStepper miniSteppers[3] = {
        {"Start", &drafts[i].startMinutes, 15, 0, 1425, formatTime, 0},
        {"End", &drafts[i].endMinutes, 15, 0, 1425, formatTime, 2},
        {"Target", &drafts[i].targetTemperatureF, 1, 40, 90, formatTemperatureF, 4},
    };

    for (size_t s = 0; s < 3; s++)
    {
      lv_obj_t *column = lv_obj_create(row);
      lv_obj_remove_style_all(column);
      lv_obj_set_flex_flow(column, LV_FLEX_FLOW_COLUMN);
      lv_obj_set_flex_align(column, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER);

      lv_obj_t *caption = lv_label_create(column);
      lv_label_set_text(caption, miniSteppers[s].label);
      lv_obj_set_style_text_color(caption, UiTheme::textMuted(), 0);
      lv_obj_set_style_text_font(caption, UiTheme::fontBody(), 0);

      lv_obj_t *stepper = lv_obj_create(column);
      lv_obj_set_size(stepper, 150, 44);
      lv_obj_set_style_bg_color(stepper, UiTheme::bgPanel(), 0);
      lv_obj_set_style_radius(stepper, 22, 0);
      lv_obj_set_style_pad_all(stepper, 2, 0);
      lv_obj_set_flex_flow(stepper, LV_FLEX_FLOW_ROW);
      lv_obj_set_flex_align(stepper, LV_FLEX_ALIGN_SPACE_BETWEEN, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER);

      lv_obj_t *minusButton = lv_button_create(stepper);
      lv_obj_set_size(minusButton, 36, 36);
      lv_obj_t *minusLabel = lv_label_create(minusButton);
      lv_label_set_text(minusLabel, "-");
      lv_obj_center(minusLabel);

      lv_obj_t *valueLabel = lv_label_create(stepper);
      lv_obj_set_flex_grow(valueLabel, 1);
      lv_obj_set_style_text_align(valueLabel, LV_TEXT_ALIGN_CENTER, 0);
      lv_obj_set_style_text_color(valueLabel, UiTheme::textMain(), 0);
      char buffer[24];
      miniSteppers[s].format(*miniSteppers[s].value, buffer, sizeof(buffer));
      lv_label_set_text(valueLabel, buffer);

      lv_obj_t *plusButton = lv_button_create(stepper);
      lv_obj_set_size(plusButton, 36, 36);
      lv_obj_t *plusLabel = lv_label_create(plusButton);
      lv_label_set_text(plusLabel, "+");
      lv_obj_center(plusLabel);

      StepperBinding *minusBinding = &ctx->steppers[miniSteppers[s].bindingOffset];
      StepperBinding *plusBinding = &ctx->steppers[miniSteppers[s].bindingOffset + 1];
      *minusBinding = {miniSteppers[s].value, miniSteppers[s].step, miniSteppers[s].minValue, miniSteppers[s].maxValue, -1, valueLabel, miniSteppers[s].format};
      *plusBinding = {miniSteppers[s].value, miniSteppers[s].step, miniSteppers[s].minValue, miniSteppers[s].maxValue, 1, valueLabel, miniSteppers[s].format};
      lv_obj_add_event_cb(minusButton, handleStepperClicked, LV_EVENT_CLICKED, minusBinding);
      lv_obj_add_event_cb(plusButton, handleStepperClicked, LV_EVENT_CLICKED, plusBinding);
    }

    lv_obj_t *removeButton = lv_button_create(row);
    lv_obj_set_size(removeButton, 44, 44);
    lv_obj_set_style_bg_color(removeButton, UiTheme::accentDanger(), 0);
    lv_obj_t *removeLabel = lv_label_create(removeButton);
    lv_label_set_text(removeLabel, "X");
    lv_obj_set_style_text_color(removeLabel, UiTheme::textOnPrimaryStrong(), 0);
    lv_obj_center(removeLabel);
    lv_obj_add_event_cb(removeButton, handleRemoveScheduleClicked, LV_EVENT_CLICKED, ctx);
  }
}

void ThermostatEditor::addSchedule(RunType type)
{
  ScheduleDraft *drafts = draftsFor(type);
  size_t *count = draftCountFor(type);
  if (*count >= MAX_SCHEDULES)
  {
    return;
  }

  ScheduleDraft draft;
  draft.hasId = false;
  draft.startMinutes = 0;
  draft.endMinutes = 60;
  draft.targetTemperatureF = type == RunType::Heating ? heatingSetPointF : coolingSetPointF;
  drafts[*count] = draft;
  (*count)++;

  rebuildScheduleList(type);
}

void ThermostatEditor::removeSchedule(RunType type, size_t index)
{
  ScheduleDraft *drafts = draftsFor(type);
  size_t *count = draftCountFor(type);
  if (index >= *count)
  {
    return;
  }

  if (drafts[index].hasId && removedScheduleCount < MAX_SCHEDULES)
  {
    memcpy(removedScheduleIds[removedScheduleCount], drafts[index].id, 16);
    removedScheduleCount++;
  }

  for (size_t i = index; i + 1 < *count; i++)
  {
    drafts[i] = drafts[i + 1];
  }
  (*count)--;

  rebuildScheduleList(type);
}

void ThermostatEditor::open(const ThermostatState &state)
{
  const SetPointState *heatingSetPoint = state.findSetPoint(RunType::Heating);
  const SetPointState *coolingSetPoint = state.findSetPoint(RunType::Cooling);
  heatingSetPointF = heatingSetPoint ? TemperatureFormat::celsiusToWholeFahrenheit(heatingSetPoint->targetTemperatureC) : 68;
  coolingSetPointF = coolingSetPoint ? TemperatureFormat::celsiusToWholeFahrenheit(coolingSetPoint->targetTemperatureC) : 75;

  char buffer[24];
  formatTemperatureF(heatingSetPointF, buffer, sizeof(buffer));
  lv_label_set_text(heatingValueLabel, buffer);
  formatTemperatureF(coolingSetPointF, buffer, sizeof(buffer));
  lv_label_set_text(coolingValueLabel, buffer);

  bool isHeatingAvailable = state.mode == RunMode::Heating || state.mode == RunMode::Automatic;
  bool isCoolingAvailable = state.mode == RunMode::Cooling || state.mode == RunMode::Automatic;
  isHeatingAvailable ? lv_obj_remove_flag(heatingRow, LV_OBJ_FLAG_HIDDEN) : lv_obj_add_flag(heatingRow, LV_OBJ_FLAG_HIDDEN);
  isCoolingAvailable ? lv_obj_remove_flag(coolingRow, LV_OBJ_FLAG_HIDDEN) : lv_obj_add_flag(coolingRow, LV_OBJ_FLAG_HIDDEN);

  heatScheduleCount = 0;
  coolScheduleCount = 0;
  removedScheduleCount = 0;

  for (size_t i = 0; i < state.scheduleCount; i++)
  {
    const ScheduleState &schedule = state.schedules[i];
    size_t *count = draftCountFor(schedule.type);
    if (*count >= MAX_SCHEDULES)
    {
      continue;
    }

    ScheduleDraft draft;
    draft.hasId = true;
    memcpy(draft.id, schedule.id, sizeof(draft.id));
    draft.startMinutes = schedule.startMinutes;
    draft.endMinutes = schedule.endMinutes;
    draft.targetTemperatureF = TemperatureFormat::celsiusToWholeFahrenheit(schedule.targetTemperatureC);

    draftsFor(schedule.type)[*count] = draft;
    (*count)++;
  }

  rebuildScheduleList(RunType::Heating);
  rebuildScheduleList(RunType::Cooling);

  lv_tabview_set_active(tabview, 0, LV_ANIM_OFF);
  lv_obj_remove_flag(overlay, LV_OBJ_FLAG_HIDDEN);
}

void ThermostatEditor::close()
{
  lv_obj_add_flag(overlay, LV_OBJ_FLAG_HIDDEN);
}

void ThermostatEditor::handleSave()
{
  bool isHeatingAvailable = !lv_obj_has_flag(heatingRow, LV_OBJ_FLAG_HIDDEN);
  bool isCoolingAvailable = !lv_obj_has_flag(coolingRow, LV_OBJ_FLAG_HIDDEN);

  if (isHeatingAvailable)
  {
    ui->onSetPointAdjusted(RunType::Heating, TemperatureFormat::wholeFahrenheitToCelsius(heatingSetPointF));
  }
  if (isCoolingAvailable)
  {
    ui->onSetPointAdjusted(RunType::Cooling, TemperatureFormat::wholeFahrenheitToCelsius(coolingSetPointF));
  }

  for (size_t i = 0; i < heatScheduleCount; i++)
  {
    const ScheduleDraft &draft = heatSchedules[i];
    ui->onUpsertSchedule(
        draft.hasId ? draft.id : nullptr,
        RunType::Heating,
        (uint16_t)draft.startMinutes,
        (uint16_t)draft.endMinutes,
        TemperatureFormat::wholeFahrenheitToCelsius(draft.targetTemperatureF));
  }
  for (size_t i = 0; i < coolScheduleCount; i++)
  {
    const ScheduleDraft &draft = coolSchedules[i];
    ui->onUpsertSchedule(
        draft.hasId ? draft.id : nullptr,
        RunType::Cooling,
        (uint16_t)draft.startMinutes,
        (uint16_t)draft.endMinutes,
        TemperatureFormat::wholeFahrenheitToCelsius(draft.targetTemperatureF));
  }

  for (size_t i = 0; i < removedScheduleCount; i++)
  {
    ui->onRemoveSchedule(removedScheduleIds[i]);
  }

  close();
}

void ThermostatEditor::handleCancel()
{
  close();
}

void ThermostatEditor::handleStepperClicked(lv_event_t *e)
{
  auto *binding = (StepperBinding *)lv_event_get_user_data(e);
  int32_t updated = *binding->target + binding->direction * binding->step;
  if (updated < binding->minValue)
    updated = binding->minValue;
  if (updated > binding->maxValue)
    updated = binding->maxValue;
  *binding->target = updated;

  char buffer[24];
  binding->format(updated, buffer, sizeof(buffer));
  lv_label_set_text(binding->valueLabel, buffer);
}

void ThermostatEditor::handleSaveClicked(lv_event_t *e)
{
  ((ThermostatEditor *)lv_event_get_user_data(e))->handleSave();
}

void ThermostatEditor::handleCancelClicked(lv_event_t *e)
{
  ((ThermostatEditor *)lv_event_get_user_data(e))->handleCancel();
}

void ThermostatEditor::handleAddHeatScheduleClicked(lv_event_t *e)
{
  ((ThermostatEditor *)lv_event_get_user_data(e))->addSchedule(RunType::Heating);
}

void ThermostatEditor::handleAddCoolScheduleClicked(lv_event_t *e)
{
  ((ThermostatEditor *)lv_event_get_user_data(e))->addSchedule(RunType::Cooling);
}

void ThermostatEditor::handleRemoveScheduleClicked(lv_event_t *e)
{
  auto *ctx = (RowContext *)lv_event_get_user_data(e);
  ctx->editor->removeSchedule(ctx->type, ctx->index);
}

void ThermostatEditor::freeRowContext(lv_event_t *e)
{
  delete (RowContext *)lv_event_get_user_data(e);
}
