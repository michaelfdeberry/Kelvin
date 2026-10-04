#pragma once

#include <esp_lcd_panel_ops.h>
#include <esp_lcd_panel_rgb.h>

// Drives the ESP32-S3-Touch-LCD-7B's parallel RGB panel directly through ESP-IDF's esp_lcd, mirroring the
// official demo's rgb_lcd_port.cpp (examples/Arduino/examples/16_LVGL_V9_DEMO in
// github.com/waveshareteam/ESP32-S3-Touch-LCD-7B). ESP32_Display_Panel isn't used: its I2C host code uses the
// legacy ESP-IDF I2C driver, which aborts at boot ("CONFLICT! driver_ng ...") when linked alongside Wire on
// arduino-esp32 3.3.x, and it is linked whole-archive so it can't be configured away.
class RgbPanel
{
public:
  // Returns false (and leaves the panel unusable) if the driver couldn't be installed.
  bool begin();

  void draw(int xStart, int yStart, int xEnd, int yEnd, const void *pixels);

private:
  esp_lcd_panel_handle_t handle = nullptr;
};
