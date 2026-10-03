#pragma once

#include <lvgl.h>

// Mirrors Kelvin.Client's app/styles.css `:root` design tokens (see the kelvin-client repo memory note) so
// the panel's look and feel stays consistent with the web app, adjusted only where the embedded display
// needs it (e.g. no CSS custom properties, so these are plain functions instead).
namespace UiTheme
{
  inline lv_color_t bgDark() { return lv_color_hex(0x0f172a); }
  inline lv_color_t bgPanel() { return lv_color_hex(0x1e293b); }
  inline lv_color_t textMain() { return lv_color_hex(0xf8fafc); }
  inline lv_color_t textMuted() { return lv_color_hex(0x94a3b8); }
  inline lv_color_t accentHeat() { return lv_color_hex(0xf97316); }
  inline lv_color_t accentCool() { return lv_color_hex(0x3b82f6); }
  inline lv_color_t accentIdle() { return lv_color_hex(0x475569); }
  inline lv_color_t borderSubtle() { return lv_color_hex(0x334155); }
  inline lv_color_t accentPrimary() { return lv_color_hex(0x2563eb); }
  inline lv_color_t accentPrimaryStrong() { return lv_color_hex(0x1d4ed8); }
  inline lv_color_t accentSuccess() { return lv_color_hex(0x22c55e); }
  inline lv_color_t accentDanger() { return lv_color_hex(0xef4444); }
  inline lv_color_t accentDangerStrong() { return lv_color_hex(0xb91c1c); }
  inline lv_color_t textOnPrimaryStrong() { return lv_color_hex(0xeff6ff); }

  // Only Montserrat 14/20/32/48 are enabled in lv_conf.h (see Hmi.bat) - keep to these four sizes.
  inline const lv_font_t *fontBody() { return &lv_font_montserrat_14; }
  inline const lv_font_t *fontLabel() { return &lv_font_montserrat_20; }
  inline const lv_font_t *fontValue() { return &lv_font_montserrat_32; }
  inline const lv_font_t *fontHero() { return &lv_font_montserrat_48; }
}
