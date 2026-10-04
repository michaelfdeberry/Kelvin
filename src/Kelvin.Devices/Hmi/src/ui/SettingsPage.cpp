#include <stdio.h>
#include <string.h>
#include "SettingsPage.h"
#include "Ui.h"
#include "UiTheme.h"
#include "UiWidgets.h"
#include "TemperatureFormat.h"
#include "Config.h"

namespace
{
  const int PAGE_PADDING = 24;
  const int COLUMN_GAP = 16;
  const int HEADER_HEIGHT = 64;
  const int TAB_HEIGHT = 56;
  const int BODY_PAD_TOP = 14;
  const int BODY_PAD_BOTTOM = 20;
  const int BODY_GAP = 14;
  const int CONTENT_HEIGHT = DISPLAY_VERTICAL_RESOLUTION - HEADER_HEIGHT - BODY_PAD_TOP - BODY_PAD_BOTTOM - TAB_HEIGHT - BODY_GAP;
  const int CARD_PADDING = 18;
  const int CARD_WIDTH = (DISPLAY_HORIZONTAL_RESOLUTION - 2 * PAGE_PADDING - COLUMN_GAP) / 2;
  const int TIMELINE_WIDTH = CARD_WIDTH - 2 * CARD_PADDING;
  const int TIMELINE_HEIGHT = 14;
  const int MINUTES_PER_DAY = 1440;

  const int TARGET_MIN_F = 40;
  const int TARGET_MAX_F = 90;
  const int LOCKOUT_MIN_F = 20;
  const int LOCKOUT_MAX_F = 100;
  const int DEFAULT_HEATING_LOCKOUT_F = 70;
  const int DEFAULT_COOLING_LOCKOUT_F = 55;

  lv_color_t accentFor(size_t column)
  {
    return column == 0 ? UiTheme::accentHeat() : UiTheme::accentCool();
  }

  int32_t clampTo(int32_t value, int32_t low, int32_t high)
  {
    return value < low ? low : (value > high ? high : value);
  }

  void formatTime12h(int32_t minutesOfDay, char *buffer, size_t bufferSize)
  {
    int32_t hours = (minutesOfDay / 60) % 24;
    int32_t minutes = minutesOfDay % 60;
    int32_t displayHour = hours % 12 == 0 ? 12 : hours % 12;
    snprintf(buffer, bufferSize, "%ld:%02ld %s", (long)displayHour, (long)minutes, hours < 12 ? "AM" : "PM");
  }

  void formatTemperatureF(int32_t fahrenheit, char *buffer, size_t bufferSize)
  {
    snprintf(buffer, bufferSize, "%ld\u00B0F", (long)fahrenheit);
  }

  lv_obj_t *createCard(lv_obj_t *parent)
  {
    lv_obj_t *card = lv_obj_create(parent);
    lv_obj_remove_style_all(card);
    lv_obj_set_size(card, 0, CONTENT_HEIGHT);
    lv_obj_set_flex_grow(card, 1);
    lv_obj_set_style_bg_color(card, UiTheme::bgPanel(), 0);
    lv_obj_set_style_bg_opa(card, LV_OPA_COVER, 0);
    lv_obj_set_style_radius(card, 14, 0);
    lv_obj_set_style_border_width(card, 1, 0);
    lv_obj_set_style_border_color(card, UiTheme::borderSubtle(), 0);
    lv_obj_set_style_pad_all(card, CARD_PADDING, 0);
    lv_obj_set_style_pad_gap(card, 12, 0);
    lv_obj_set_flex_flow(card, LV_FLEX_FLOW_COLUMN);
    lv_obj_remove_flag(card, LV_OBJ_FLAG_SCROLLABLE);
    return card;
  }

  lv_obj_t *createRow(lv_obj_t *parent, int32_t width, int32_t height, lv_flex_align_t mainPlace)
  {
    lv_obj_t *row = lv_obj_create(parent);
    lv_obj_remove_style_all(row);
    lv_obj_set_size(row, width, height);
    lv_obj_set_flex_flow(row, LV_FLEX_FLOW_ROW);
    lv_obj_set_flex_align(row, mainPlace, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER);
    lv_obj_remove_flag(row, LV_OBJ_FLAG_SCROLLABLE);
    return row;
  }

  lv_obj_t *createLabel(lv_obj_t *parent, const char *text, const lv_font_t *font, lv_color_t color)
  {
    lv_obj_t *label = lv_label_create(parent);
    lv_label_set_text(label, text);
    lv_obj_set_style_text_font(label, font, 0);
    lv_obj_set_style_text_color(label, color, 0);
    return label;
  }

  // Slider centred in a container inset by the knob radius, so the large knob never clips the card edge.
  lv_obj_t *createSliderHolder(lv_obj_t *parent)
  {
    lv_obj_t *holder = lv_obj_create(parent);
    lv_obj_remove_style_all(holder);
    lv_obj_set_size(holder, LV_PCT(100), LV_SIZE_CONTENT);
    lv_obj_set_style_pad_hor(holder, 40, 0);
    lv_obj_set_style_pad_ver(holder, 34, 0);
    lv_obj_remove_flag(holder, LV_OBJ_FLAG_SCROLLABLE);
    return holder;
  }

  lv_obj_t *createRoller(lv_obj_t *parent, const char *options, bool infinite)
  {
    lv_obj_t *roller = lv_roller_create(parent);
    lv_roller_set_options(roller, options, infinite ? LV_ROLLER_MODE_INFINITE : LV_ROLLER_MODE_NORMAL);
    lv_roller_set_visible_row_count(roller, 3);
    lv_obj_set_width(roller, 92);
    lv_obj_set_style_bg_color(roller, UiTheme::bgDark(), LV_PART_MAIN);
    lv_obj_set_style_bg_opa(roller, LV_OPA_COVER, LV_PART_MAIN);
    lv_obj_set_style_border_color(roller, UiTheme::borderSubtle(), LV_PART_MAIN);
    lv_obj_set_style_border_width(roller, 1, LV_PART_MAIN);
    lv_obj_set_style_radius(roller, 12, LV_PART_MAIN);
    lv_obj_set_style_text_font(roller, UiTheme::fontValue(), LV_PART_MAIN);
    lv_obj_set_style_text_color(roller, UiTheme::textMuted(), LV_PART_MAIN);
    lv_obj_set_style_text_font(roller, UiTheme::fontValue(), LV_PART_SELECTED);
    lv_obj_set_style_text_color(roller, UiTheme::textMain(), LV_PART_SELECTED);
    lv_obj_set_style_bg_color(roller, UiTheme::accentPrimary(), LV_PART_SELECTED);
    lv_obj_set_style_bg_opa(roller, LV_OPA_COVER, LV_PART_SELECTED);
    return roller;
  }

  void setTimeRollers(lv_obj_t *const rollers[3], int32_t minutesOfDay)
  {
    int32_t hours = (minutesOfDay / 60) % 24;
    lv_roller_set_selected(rollers[0], hours % 12, LV_ANIM_OFF);
    lv_roller_set_selected(rollers[1], (minutesOfDay % 60) / 15, LV_ANIM_OFF);
    lv_roller_set_selected(rollers[2], hours >= 12 ? 1 : 0, LV_ANIM_OFF);
  }

  int32_t readTimeRollers(lv_obj_t *const rollers[3])
  {
    int32_t hours = lv_roller_get_selected(rollers[2]) * 12 + lv_roller_get_selected(rollers[0]);
    return hours * 60 + lv_roller_get_selected(rollers[1]) * 15;
  }
}

void SettingsPage::begin(Ui &uiRef)
{
  ui = &uiRef;

  page = lv_obj_create(nullptr);
  lv_obj_remove_style_all(page);
  lv_obj_set_style_bg_color(page, UiTheme::bgDark(), 0);
  lv_obj_set_style_bg_opa(page, LV_OPA_COVER, 0);
  lv_obj_remove_flag(page, LV_OBJ_FLAG_SCROLLABLE);
  lv_obj_set_flex_flow(page, LV_FLEX_FLOW_COLUMN);

  buildHeader(page);

  lv_obj_t *body = lv_obj_create(page);
  lv_obj_remove_style_all(body);
  lv_obj_set_width(body, LV_PCT(100));
  lv_obj_set_flex_grow(body, 1);
  lv_obj_set_style_pad_hor(body, PAGE_PADDING, 0);
  lv_obj_set_style_pad_top(body, BODY_PAD_TOP, 0);
  lv_obj_set_style_pad_bottom(body, BODY_PAD_BOTTOM, 0);
  lv_obj_set_style_pad_gap(body, BODY_GAP, 0);
  lv_obj_set_flex_flow(body, LV_FLEX_FLOW_COLUMN);
  lv_obj_remove_flag(body, LV_OBJ_FLAG_SCROLLABLE);

  buildTabs(body);
  buildScheduleTab(body);
  buildLockoutTab(body);
  buildSheet(page);

  showTab(0);
}

void SettingsPage::buildHeader(lv_obj_t *parent)
{
  lv_obj_t *bar = lv_obj_create(parent);
  lv_obj_remove_style_all(bar);
  lv_obj_set_size(bar, LV_PCT(100), HEADER_HEIGHT);
  lv_obj_set_style_pad_hor(bar, 16, 0);
  lv_obj_set_style_pad_gap(bar, 16, 0);
  lv_obj_set_style_border_side(bar, LV_BORDER_SIDE_BOTTOM, 0);
  lv_obj_set_style_border_width(bar, 1, 0);
  lv_obj_set_style_border_color(bar, UiTheme::borderSubtle(), 0);
  lv_obj_set_flex_flow(bar, LV_FLEX_FLOW_ROW);
  lv_obj_set_flex_align(bar, LV_FLEX_ALIGN_START, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER);
  lv_obj_remove_flag(bar, LV_OBJ_FLAG_SCROLLABLE);

  lv_obj_t *back = UiWidgets::createSolidButton(bar, LV_SYMBOL_LEFT "  Back", 130, 44, UiTheme::bgPanel(), UiTheme::borderSubtle(), UiTheme::textMain());
  lv_obj_add_event_cb(back, handleBackClicked, LV_EVENT_CLICKED, this);

  createLabel(bar, "Settings", UiTheme::fontValue(), UiTheme::textMain());
}

void SettingsPage::buildTabs(lv_obj_t *parent)
{
  lv_obj_t *segments = lv_obj_create(parent);
  lv_obj_remove_style_all(segments);
  lv_obj_set_size(segments, 460, TAB_HEIGHT);
  lv_obj_set_style_pad_all(segments, 5, 0);
  lv_obj_set_style_pad_gap(segments, 5, 0);
  lv_obj_set_style_radius(segments, 16, 0);
  lv_obj_set_style_bg_color(segments, UiTheme::bgPanel(), 0);
  lv_obj_set_style_bg_opa(segments, LV_OPA_COVER, 0);
  lv_obj_set_style_border_width(segments, 1, 0);
  lv_obj_set_style_border_color(segments, UiTheme::borderSubtle(), 0);
  lv_obj_set_flex_flow(segments, LV_FLEX_FLOW_ROW);
  lv_obj_remove_flag(segments, LV_OBJ_FLAG_SCROLLABLE);

  static const char *labels[2] = {"Schedules", "Forecast Lockout"};
  for (size_t i = 0; i < 2; i++)
  {
    tabButtons[i] = UiWidgets::createFlatButton(segments, labels[i], 12);
    lv_obj_set_flex_grow(tabButtons[i], 1);
    tabBindings[i] = {this, i};
    lv_obj_add_event_cb(tabButtons[i], handleTabClicked, LV_EVENT_CLICKED, &tabBindings[i]);
  }
}

void SettingsPage::showTab(size_t index)
{
  if (index == 0)
  {
    lv_obj_remove_flag(scheduleTab, LV_OBJ_FLAG_HIDDEN);
    lv_obj_add_flag(lockoutTab, LV_OBJ_FLAG_HIDDEN);
  }
  else
  {
    lv_obj_add_flag(scheduleTab, LV_OBJ_FLAG_HIDDEN);
    lv_obj_remove_flag(lockoutTab, LV_OBJ_FLAG_HIDDEN);
  }

  for (size_t i = 0; i < 2; i++)
  {
    bool selected = i == index;
    lv_obj_set_style_bg_color(tabButtons[i], UiTheme::accentPrimary(), 0);
    lv_obj_set_style_bg_opa(tabButtons[i], selected ? LV_OPA_COVER : LV_OPA_TRANSP, 0);
    lv_obj_set_style_text_color(lv_obj_get_child(tabButtons[i], 0), selected ? UiTheme::textMain() : UiTheme::textMuted(), 0);
  }
}

void SettingsPage::buildScheduleTab(lv_obj_t *parent)
{
  scheduleTab = createRow(parent, LV_PCT(100), CONTENT_HEIGHT, LV_FLEX_ALIGN_START);
  lv_obj_set_style_pad_gap(scheduleTab, COLUMN_GAP, 0);
  lv_obj_set_flex_align(scheduleTab, LV_FLEX_ALIGN_START, LV_FLEX_ALIGN_START, LV_FLEX_ALIGN_START);

  buildScheduleColumn(scheduleTab, 0);
  buildScheduleColumn(scheduleTab, 1);
}

void SettingsPage::buildScheduleColumn(lv_obj_t *parent, size_t index)
{
  lv_color_t accent = accentFor(index);
  lv_obj_t *card = createCard(parent);

  lv_obj_t *header = createRow(card, LV_PCT(100), 44, LV_FLEX_ALIGN_SPACE_BETWEEN);
  createLabel(header, index == 0 ? "Heating" : "Cooling", UiTheme::fontValue(), accent);

  columnBindings[index] = {this, index};
  lv_obj_t *addButton = UiWidgets::createSolidButton(header, "+ Add", 110, 44, UiTheme::bgDark(), accent, accent);
  lv_obj_add_event_cb(addButton, handleAddClicked, LV_EVENT_CLICKED, &columnBindings[index]);

  // 24h overview of where this mode's schedule blocks fall.
  timelines[index] = lv_obj_create(card);
  lv_obj_remove_style_all(timelines[index]);
  lv_obj_set_size(timelines[index], TIMELINE_WIDTH, TIMELINE_HEIGHT);
  lv_obj_set_style_bg_color(timelines[index], UiTheme::bgDark(), 0);
  lv_obj_set_style_bg_opa(timelines[index], LV_OPA_COVER, 0);
  lv_obj_set_style_radius(timelines[index], TIMELINE_HEIGHT / 2, 0);
  lv_obj_set_style_clip_corner(timelines[index], true, 0);
  lv_obj_remove_flag(timelines[index], LV_OBJ_FLAG_SCROLLABLE);

  lv_obj_t *ticks = createRow(card, LV_PCT(100), LV_SIZE_CONTENT, LV_FLEX_ALIGN_SPACE_BETWEEN);
  static const char *tickLabels[5] = {"12a", "6a", "12p", "6p", "12a"};
  for (size_t i = 0; i < 5; i++)
  {
    createLabel(ticks, tickLabels[i], UiTheme::fontBody(), UiTheme::textMuted());
  }

  lists[index] = lv_obj_create(card);
  lv_obj_remove_style_all(lists[index]);
  lv_obj_set_width(lists[index], LV_PCT(100));
  lv_obj_set_flex_grow(lists[index], 1);
  lv_obj_set_style_pad_gap(lists[index], 8, 0);
  lv_obj_set_flex_flow(lists[index], LV_FLEX_FLOW_COLUMN);
  lv_obj_set_scroll_dir(lists[index], LV_DIR_VER);
}

void SettingsPage::rebuildSchedules()
{
  for (size_t column = 0; column < 2; column++)
  {
    lv_obj_clean(timelines[column]);
    lv_obj_clean(lists[column]);

    RunType type = column == 0 ? RunType::Heating : RunType::Cooling;
    size_t order[MAX_SCHEDULES];
    size_t count = 0;
    for (size_t i = 0; i < latest.scheduleCount; i++)
    {
      if (latest.schedules[i].type == type)
      {
        order[count++] = i;
      }
    }

    for (size_t i = 1; i < count; i++)
    {
      size_t current = order[i];
      size_t j = i;
      while (j > 0 && latest.schedules[order[j - 1]].startMinutes > latest.schedules[current].startMinutes)
      {
        order[j] = order[j - 1];
        j--;
      }
      order[j] = current;
    }

    for (size_t i = 0; i < count; i++)
    {
      addScheduleRow(column, order[i]);
    }

    if (count == 0)
    {
      createLabel(lists[column], "No schedules - the set point is used all day.", UiTheme::fontBody(), UiTheme::textMuted());
    }
  }
}

void SettingsPage::addScheduleRow(size_t column, size_t scheduleIndex)
{
  const ScheduleState &schedule = latest.schedules[scheduleIndex];
  lv_color_t accent = accentFor(column);

  auto addBlock = [&](int32_t fromMinutes, int32_t toMinutes)
  {
    int32_t x = fromMinutes * TIMELINE_WIDTH / MINUTES_PER_DAY;
    int32_t width = (toMinutes - fromMinutes) * TIMELINE_WIDTH / MINUTES_PER_DAY;
    lv_obj_t *block = lv_obj_create(timelines[column]);
    lv_obj_remove_style_all(block);
    lv_obj_set_pos(block, x, 0);
    lv_obj_set_size(block, width < 3 ? 3 : width, TIMELINE_HEIGHT);
    lv_obj_set_style_bg_color(block, accent, 0);
    lv_obj_set_style_bg_opa(block, LV_OPA_COVER, 0);
  };

  // An end at or before the start means the block wraps past midnight.
  if (schedule.endMinutes > schedule.startMinutes)
  {
    addBlock(schedule.startMinutes, schedule.endMinutes);
  }
  else
  {
    addBlock(schedule.startMinutes, MINUTES_PER_DAY);
    addBlock(0, schedule.endMinutes);
  }

  lv_obj_t *row = createRow(lists[column], LV_PCT(100), 60, LV_FLEX_ALIGN_SPACE_BETWEEN);
  lv_obj_set_style_pad_hor(row, 16, 0);
  lv_obj_set_style_bg_color(row, UiTheme::bgDark(), 0);
  lv_obj_set_style_bg_opa(row, LV_OPA_COVER, 0);
  lv_obj_set_style_radius(row, 12, 0);
  lv_obj_set_style_border_width(row, 1, 0);
  lv_obj_set_style_border_color(row, UiTheme::borderSubtle(), 0);
  lv_obj_set_style_opa(row, LV_OPA_70, LV_STATE_PRESSED);
  lv_obj_add_flag(row, LV_OBJ_FLAG_CLICKABLE);
  lv_obj_remove_flag(row, LV_OBJ_FLAG_SCROLL_CHAIN_HOR);

  char text[40];
  char start[16];
  char end[16];
  formatTime12h(schedule.startMinutes, start, sizeof(start));
  formatTime12h(schedule.endMinutes, end, sizeof(end));
  snprintf(text, sizeof(text), "%s - %s", start, end);
  createLabel(row, text, UiTheme::fontLabel(), UiTheme::textMain());

  formatTemperatureF(TemperatureFormat::celsiusToWholeFahrenheit(schedule.targetTemperatureC), text, sizeof(text));
  createLabel(row, text, UiTheme::fontLabel(), accent);

  rowBindings[scheduleIndex] = {this, scheduleIndex};
  lv_obj_add_event_cb(row, handleRowClicked, LV_EVENT_CLICKED, &rowBindings[scheduleIndex]);
}

void SettingsPage::buildLockoutTab(lv_obj_t *parent)
{
  lockoutTab = createRow(parent, LV_PCT(100), CONTENT_HEIGHT, LV_FLEX_ALIGN_START);
  lv_obj_set_style_pad_gap(lockoutTab, COLUMN_GAP, 0);
  lv_obj_set_flex_align(lockoutTab, LV_FLEX_ALIGN_START, LV_FLEX_ALIGN_START, LV_FLEX_ALIGN_START);

  buildLockoutCard(lockoutTab, 0);
  buildLockoutCard(lockoutTab, 1);
}

void SettingsPage::buildLockoutCard(lv_obj_t *parent, size_t index)
{
  bool heating = index == 0;
  lv_color_t accent = accentFor(index);
  lv_obj_t *card = createCard(parent);
  lockoutBindings[index] = {this, index};

  lv_obj_t *header = createRow(card, LV_PCT(100), 48, LV_FLEX_ALIGN_SPACE_BETWEEN);
  createLabel(header, heating ? "Heating lockout" : "Cooling lockout", UiTheme::fontValue(), accent);

  lockoutSwitches[index] = lv_switch_create(header);
  lv_obj_set_size(lockoutSwitches[index], 80, 44);
  lv_obj_set_style_bg_color(lockoutSwitches[index], UiTheme::bgDark(), LV_PART_MAIN);
  lv_obj_set_style_bg_color(lockoutSwitches[index], accent, (lv_style_selector_t)LV_PART_INDICATOR | LV_STATE_CHECKED);
  lv_obj_set_style_bg_color(lockoutSwitches[index], UiTheme::textMain(), LV_PART_KNOB);
  lv_obj_add_event_cb(lockoutSwitches[index], handleLockoutSwitch, LV_EVENT_VALUE_CHANGED, &lockoutBindings[index]);

  lv_obj_t *description = createLabel(
      card,
      heating ? "Don't run the heat while the outdoor forecast is at or above this temperature."
              : "Don't run the A/C while the outdoor forecast is at or below this temperature.",
      UiTheme::fontBody(),
      UiTheme::textMuted());
  lv_obj_set_width(description, LV_PCT(100));
  lv_label_set_long_mode(description, LV_LABEL_LONG_WRAP);

  lockoutValueLabels[index] = createLabel(card, "", UiTheme::fontHero(), accent);
  lv_obj_set_width(lockoutValueLabels[index], LV_PCT(100));
  lv_obj_set_style_text_align(lockoutValueLabels[index], LV_TEXT_ALIGN_CENTER, 0);

  lv_obj_t *holder = createSliderHolder(card);
  lockoutSliders[index] = lv_slider_create(holder);
  lv_obj_set_width(lockoutSliders[index], LV_PCT(100));
  lv_slider_set_range(lockoutSliders[index], LOCKOUT_MIN_F, LOCKOUT_MAX_F);
  UiWidgets::styleSlider(lockoutSliders[index], UiTheme::bgDark(), accent);
  lv_obj_add_event_cb(lockoutSliders[index], handleLockoutSlider, LV_EVENT_ALL, &lockoutBindings[index]);

  lv_obj_t *ends = createRow(card, LV_PCT(100), LV_SIZE_CONTENT, LV_FLEX_ALIGN_SPACE_BETWEEN);
  lv_obj_set_style_pad_hor(ends, 22, 0);
  char text[16];
  formatTemperatureF(LOCKOUT_MIN_F, text, sizeof(text));
  createLabel(ends, text, UiTheme::fontBody(), UiTheme::textMuted());
  formatTemperatureF(LOCKOUT_MAX_F, text, sizeof(text));
  createLabel(ends, text, UiTheme::fontBody(), UiTheme::textMuted());
}

void SettingsPage::refreshLockoutDimming(size_t index)
{
  bool enabled = lockoutEnabled[index];
  if (enabled)
  {
    lv_obj_add_state(lockoutSwitches[index], LV_STATE_CHECKED);
    lv_obj_remove_state(lockoutSliders[index], LV_STATE_DISABLED);
  }
  else
  {
    lv_obj_remove_state(lockoutSwitches[index], LV_STATE_CHECKED);
    lv_obj_add_state(lockoutSliders[index], LV_STATE_DISABLED);
  }
  lv_obj_set_style_opa(lockoutSliders[index], enabled ? LV_OPA_COVER : LV_OPA_40, 0);
  lv_obj_set_style_text_color(lockoutValueLabels[index], enabled ? accentFor(index) : UiTheme::textMuted(), 0);

  char text[16];
  if (enabled)
  {
    formatTemperatureF(lockoutF[index], text, sizeof(text));
  }
  else
  {
    snprintf(text, sizeof(text), "Off");
  }
  lv_label_set_text(lockoutValueLabels[index], text);
  lv_slider_set_value(lockoutSliders[index], lockoutF[index], LV_ANIM_OFF);
}

void SettingsPage::refreshLockouts()
{
  // A drag in progress owns the controls - an incoming push must not snap them back mid-gesture.
  if (lockoutDragging)
  {
    return;
  }

  lockoutEnabled[0] = latest.hasHeatingLockout;
  lockoutEnabled[1] = latest.hasCoolingLockout;
  lockoutF[0] = latest.hasHeatingLockout ? clampTo(TemperatureFormat::celsiusToWholeFahrenheit(latest.heatingLockoutC), LOCKOUT_MIN_F, LOCKOUT_MAX_F)
                                         : DEFAULT_HEATING_LOCKOUT_F;
  lockoutF[1] = latest.hasCoolingLockout ? clampTo(TemperatureFormat::celsiusToWholeFahrenheit(latest.coolingLockoutC), LOCKOUT_MIN_F, LOCKOUT_MAX_F)
                                         : DEFAULT_COOLING_LOCKOUT_F;

  refreshLockoutDimming(0);
  refreshLockoutDimming(1);
}

void SettingsPage::sendLockouts()
{
  float heatingC = TemperatureFormat::wholeFahrenheitToCelsius(lockoutF[0]);
  float coolingC = TemperatureFormat::wholeFahrenheitToCelsius(lockoutF[1]);
  ui->onSetForecastLockouts(lockoutEnabled[0] ? &heatingC : nullptr, lockoutEnabled[1] ? &coolingC : nullptr);
}

lv_obj_t *SettingsPage::buildTimeRollers(lv_obj_t *parent, const char *caption, lv_obj_t *rollers[3])
{
  lv_obj_t *group = lv_obj_create(parent);
  lv_obj_remove_style_all(group);
  lv_obj_set_size(group, LV_SIZE_CONTENT, LV_SIZE_CONTENT);
  lv_obj_set_style_pad_gap(group, 8, 0);
  lv_obj_set_flex_flow(group, LV_FLEX_FLOW_COLUMN);
  lv_obj_set_flex_align(group, LV_FLEX_ALIGN_START, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER);
  lv_obj_remove_flag(group, LV_OBJ_FLAG_SCROLLABLE);

  createLabel(group, caption, UiTheme::fontLabel(), UiTheme::textMuted());

  lv_obj_t *row = createRow(group, LV_SIZE_CONTENT, LV_SIZE_CONTENT, LV_FLEX_ALIGN_START);
  lv_obj_set_style_pad_gap(row, 8, 0);
  rollers[0] = createRoller(row, "12\n1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n11", true);
  rollers[1] = createRoller(row, "00\n15\n30\n45", true);
  rollers[2] = createRoller(row, "AM\nPM", false);
  return group;
}

void SettingsPage::buildSheet(lv_obj_t *parent)
{
  sheet = lv_obj_create(parent);
  lv_obj_remove_style_all(sheet);
  lv_obj_set_size(sheet, LV_PCT(100), LV_PCT(100));
  lv_obj_set_pos(sheet, 0, 0);
  lv_obj_add_flag(sheet, LV_OBJ_FLAG_IGNORE_LAYOUT);
  lv_obj_remove_flag(sheet, LV_OBJ_FLAG_SCROLLABLE);
  lv_obj_set_style_bg_color(sheet, lv_color_black(), 0);
  lv_obj_set_style_bg_opa(sheet, LV_OPA_60, 0);
  lv_obj_set_flex_flow(sheet, LV_FLEX_FLOW_ROW);
  lv_obj_set_flex_align(sheet, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER, LV_FLEX_ALIGN_CENTER);
  lv_obj_add_flag(sheet, LV_OBJ_FLAG_HIDDEN);

  lv_obj_t *dialog = lv_obj_create(sheet);
  lv_obj_remove_style_all(dialog);
  lv_obj_set_size(dialog, 740, LV_SIZE_CONTENT);
  lv_obj_set_style_bg_color(dialog, UiTheme::bgPanel(), 0);
  lv_obj_set_style_bg_opa(dialog, LV_OPA_COVER, 0);
  lv_obj_set_style_radius(dialog, 16, 0);
  lv_obj_set_style_border_width(dialog, 1, 0);
  lv_obj_set_style_border_color(dialog, UiTheme::borderSubtle(), 0);
  lv_obj_set_style_pad_all(dialog, 22, 0);
  lv_obj_set_style_pad_gap(dialog, 14, 0);
  lv_obj_set_flex_flow(dialog, LV_FLEX_FLOW_COLUMN);
  lv_obj_remove_flag(dialog, LV_OBJ_FLAG_SCROLLABLE);

  sheetTitle = createLabel(dialog, "", UiTheme::fontValue(), UiTheme::textMain());

  lv_obj_t *times = createRow(dialog, LV_PCT(100), LV_SIZE_CONTENT, LV_FLEX_ALIGN_SPACE_EVENLY);
  buildTimeRollers(times, "Start", startRollers);
  buildTimeRollers(times, "End", endRollers);

  lv_obj_t *targetRow = createRow(dialog, LV_PCT(100), LV_SIZE_CONTENT, LV_FLEX_ALIGN_SPACE_BETWEEN);
  createLabel(targetRow, "Target temperature", UiTheme::fontLabel(), UiTheme::textMuted());
  targetLabel = createLabel(targetRow, "", UiTheme::fontValue(), UiTheme::textMain());

  lv_obj_t *holder = createSliderHolder(dialog);
  targetSlider = lv_slider_create(holder);
  lv_obj_set_width(targetSlider, LV_PCT(100));
  lv_slider_set_range(targetSlider, TARGET_MIN_F, TARGET_MAX_F);
  UiWidgets::styleSlider(targetSlider, UiTheme::bgDark(), UiTheme::accentPrimary());
  lv_obj_add_event_cb(targetSlider, handleTargetChanged, LV_EVENT_VALUE_CHANGED, this);

  sheetError = createLabel(dialog, "", UiTheme::fontBody(), UiTheme::accentDanger());

  lv_obj_t *buttons = createRow(dialog, LV_PCT(100), 56, LV_FLEX_ALIGN_START);
  lv_obj_set_style_pad_gap(buttons, 12, 0);

  deleteButton = UiWidgets::createSolidButton(buttons, "Delete", 140, 56, UiTheme::bgPanel(), UiTheme::accentDanger(), UiTheme::accentDanger());
  lv_obj_add_event_cb(deleteButton, handleSheetDelete, LV_EVENT_CLICKED, this);

  lv_obj_t *spacer = lv_obj_create(buttons);
  lv_obj_remove_style_all(spacer);
  lv_obj_set_size(spacer, 0, 1);
  lv_obj_set_flex_grow(spacer, 1);

  lv_obj_t *cancel = UiWidgets::createSolidButton(buttons, "Cancel", 140, 56, UiTheme::bgDark(), UiTheme::borderSubtle(), UiTheme::textMain());
  lv_obj_add_event_cb(cancel, handleSheetCancel, LV_EVENT_CLICKED, this);

  lv_obj_t *save = UiWidgets::createSolidButton(buttons, "Save", 140, 56, UiTheme::accentPrimary(), UiTheme::accentPrimary(), UiTheme::textOnPrimaryStrong());
  lv_obj_add_event_cb(save, handleSheetSave, LV_EVENT_CLICKED, this);
}

void SettingsPage::openSheet(RunType type, const ScheduleState *existing)
{
  editType = type;
  editHasId = existing != nullptr;

  int32_t startMinutes = 6 * 60;
  int32_t endMinutes = 22 * 60;
  int32_t targetF;
  if (existing != nullptr)
  {
    memcpy(editId, existing->id, sizeof(editId));
    startMinutes = existing->startMinutes;
    endMinutes = existing->endMinutes;
    targetF = TemperatureFormat::celsiusToWholeFahrenheit(existing->targetTemperatureC);
  }
  else
  {
    const SetPointState *setPoint = latest.findSetPoint(type);
    targetF = setPoint != nullptr ? TemperatureFormat::celsiusToWholeFahrenheit(setPoint->targetTemperatureC) : (type == RunType::Heating ? 68 : 75);
  }
  targetF = clampTo(targetF, TARGET_MIN_F, TARGET_MAX_F);

  char title[48];
  snprintf(title, sizeof(title), "%s %s schedule", existing != nullptr ? "Edit" : "New", type == RunType::Heating ? "heating" : "cooling");
  lv_label_set_text(sheetTitle, title);

  setTimeRollers(startRollers, startMinutes);
  setTimeRollers(endRollers, endMinutes);
  lv_slider_set_value(targetSlider, targetF, LV_ANIM_OFF);

  char text[16];
  formatTemperatureF(targetF, text, sizeof(text));
  lv_label_set_text(targetLabel, text);
  lv_label_set_text(sheetError, "");

  lv_color_t accent = type == RunType::Heating ? UiTheme::accentHeat() : UiTheme::accentCool();
  lv_obj_set_style_bg_color(targetSlider, accent, LV_PART_INDICATOR);
  lv_obj_set_style_border_color(targetSlider, accent, LV_PART_KNOB);
  lv_obj_set_style_text_color(targetLabel, accent, 0);

  if (editHasId)
  {
    lv_obj_remove_flag(deleteButton, LV_OBJ_FLAG_HIDDEN);
  }
  else
  {
    lv_obj_add_flag(deleteButton, LV_OBJ_FLAG_HIDDEN);
  }

  lv_obj_remove_flag(sheet, LV_OBJ_FLAG_HIDDEN);
  lv_obj_move_foreground(sheet);
}

void SettingsPage::closeSheet()
{
  lv_obj_add_flag(sheet, LV_OBJ_FLAG_HIDDEN);
}

void SettingsPage::saveSheet()
{
  int32_t startMinutes = readTimeRollers(startRollers);
  int32_t endMinutes = readTimeRollers(endRollers);
  if (startMinutes == endMinutes)
  {
    lv_label_set_text(sheetError, "Start and end must be different.");
    return;
  }

  float targetC = TemperatureFormat::wholeFahrenheitToCelsius(lv_slider_get_value(targetSlider));
  ui->onUpsertSchedule(editHasId ? editId : nullptr, editType, (uint16_t)startMinutes, (uint16_t)endMinutes, targetC);
  closeSheet();
}

void SettingsPage::deleteFromSheet()
{
  if (editHasId)
  {
    ui->onRemoveSchedule(editId);
  }
  closeSheet();
}

void SettingsPage::applyState(const ThermostatState &state)
{
  latest = state;
  rebuildSchedules();
  refreshLockouts();
}

void SettingsPage::reset()
{
  closeSheet();
  showTab(0);
}

void SettingsPage::handleBackClicked(lv_event_t *e)
{
  auto *self = (SettingsPage *)lv_event_get_user_data(e);
  self->ui->closeSettings();
}

void SettingsPage::handleTabClicked(lv_event_t *e)
{
  auto *binding = (IndexBinding *)lv_event_get_user_data(e);
  binding->page->showTab(binding->index);
}

void SettingsPage::handleAddClicked(lv_event_t *e)
{
  auto *binding = (IndexBinding *)lv_event_get_user_data(e);
  binding->page->openSheet(binding->index == 0 ? RunType::Heating : RunType::Cooling, nullptr);
}

void SettingsPage::handleRowClicked(lv_event_t *e)
{
  auto *binding = (IndexBinding *)lv_event_get_user_data(e);
  const ScheduleState &schedule = binding->page->latest.schedules[binding->index];
  binding->page->openSheet(schedule.type, &schedule);
}

void SettingsPage::handleSheetSave(lv_event_t *e)
{
  ((SettingsPage *)lv_event_get_user_data(e))->saveSheet();
}

void SettingsPage::handleSheetCancel(lv_event_t *e)
{
  ((SettingsPage *)lv_event_get_user_data(e))->closeSheet();
}

void SettingsPage::handleSheetDelete(lv_event_t *e)
{
  ((SettingsPage *)lv_event_get_user_data(e))->deleteFromSheet();
}

void SettingsPage::handleTargetChanged(lv_event_t *e)
{
  auto *self = (SettingsPage *)lv_event_get_user_data(e);
  char text[16];
  formatTemperatureF(lv_slider_get_value(self->targetSlider), text, sizeof(text));
  lv_label_set_text(self->targetLabel, text);
}

void SettingsPage::handleLockoutSwitch(lv_event_t *e)
{
  auto *binding = (IndexBinding *)lv_event_get_user_data(e);
  SettingsPage *self = binding->page;
  self->lockoutEnabled[binding->index] = lv_obj_has_state(self->lockoutSwitches[binding->index], LV_STATE_CHECKED);
  self->refreshLockoutDimming(binding->index);
  self->sendLockouts();
}

void SettingsPage::handleLockoutSlider(lv_event_t *e)
{
  auto *binding = (IndexBinding *)lv_event_get_user_data(e);
  SettingsPage *self = binding->page;
  lv_event_code_t code = lv_event_get_code(e);

  if (code == LV_EVENT_PRESSED)
  {
    self->lockoutDragging = true;
  }
  else if (code == LV_EVENT_VALUE_CHANGED)
  {
    self->lockoutF[binding->index] = lv_slider_get_value(self->lockoutSliders[binding->index]);
    char text[16];
    formatTemperatureF(self->lockoutF[binding->index], text, sizeof(text));
    lv_label_set_text(self->lockoutValueLabels[binding->index], text);
  }
  else if (code == LV_EVENT_RELEASED || code == LV_EVENT_PRESS_LOST)
  {
    // Send once on release rather than on every step of the drag, to avoid flooding the ESP-NOW link.
    self->lockoutDragging = false;
    self->sendLockouts();
  }
}
