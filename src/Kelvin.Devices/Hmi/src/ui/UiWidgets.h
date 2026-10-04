#pragma once

#include <lvgl.h>
#include "UiTheme.h"

namespace UiWidgets
{
  // Flat button with no theme gradient/shadow/outline; callers colour it via bg/opa. Sized by flex-grow by default.
  inline lv_obj_t *createFlatButton(lv_obj_t *parent, const char *text, int32_t radius, lv_obj_t **labelOut = nullptr)
  {
    lv_obj_t *button = lv_button_create(parent);
    lv_obj_remove_style_all(button);
    lv_obj_set_size(button, 0, LV_PCT(100));
    lv_obj_set_style_radius(button, radius, 0);
    lv_obj_set_style_opa(button, LV_OPA_70, LV_STATE_PRESSED);

    lv_obj_t *label = lv_label_create(button);
    lv_label_set_text(label, text);
    lv_obj_set_style_text_font(label, UiTheme::fontLabel(), 0);
    lv_obj_center(label);
    if (labelOut != nullptr)
    {
      *labelOut = label;
    }
    return button;
  }

  // A filled, bordered, fixed-size button (e.g. dialog actions).
  inline lv_obj_t *createSolidButton(lv_obj_t *parent, const char *text, int32_t width, int32_t height, lv_color_t background, lv_color_t border, lv_color_t textColor)
  {
    lv_obj_t *label = nullptr;
    lv_obj_t *button = createFlatButton(parent, text, 14, &label);
    lv_obj_set_size(button, width, height);
    lv_obj_set_style_bg_color(button, background, 0);
    lv_obj_set_style_bg_opa(button, LV_OPA_COVER, 0);
    lv_obj_set_style_border_color(button, border, 0);
    lv_obj_set_style_border_width(button, 1, 0);
    lv_obj_set_style_text_color(label, textColor, 0);
    return button;
  }

  // Thick track + large round knob, matching the main screen's set point slider.
  inline void styleSlider(lv_obj_t *slider, lv_color_t track, lv_color_t accent)
  {
    const int trackHeight = 20;
    const int knobRadius = 32;
    lv_obj_set_height(slider, trackHeight);
    lv_obj_set_ext_click_area(slider, 24);
    lv_obj_set_style_bg_color(slider, track, LV_PART_MAIN);
    lv_obj_set_style_bg_opa(slider, LV_OPA_COVER, LV_PART_MAIN);
    lv_obj_set_style_bg_color(slider, accent, LV_PART_INDICATOR);
    lv_obj_set_style_bg_opa(slider, LV_OPA_COVER, LV_PART_INDICATOR);
    lv_obj_set_style_bg_color(slider, UiTheme::textMain(), LV_PART_KNOB);
    lv_obj_set_style_bg_opa(slider, LV_OPA_COVER, LV_PART_KNOB);
    lv_obj_set_style_border_width(slider, 6, LV_PART_KNOB);
    lv_obj_set_style_border_color(slider, accent, LV_PART_KNOB);
    lv_obj_set_style_pad_all(slider, knobRadius - trackHeight / 2, LV_PART_KNOB);
  }
}
