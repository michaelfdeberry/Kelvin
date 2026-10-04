#include "RgbPanel.h"
#include <string.h>
#include <Logger.h>
#include "Config.h"

bool RgbPanel::begin()
{
  esp_lcd_rgb_panel_config_t config;
  memset(&config, 0, sizeof(config));

  config.clk_src = LCD_CLK_SRC_DEFAULT;
  config.timings.pclk_hz = 30 * 1000 * 1000;
  config.timings.h_res = DISPLAY_HORIZONTAL_RESOLUTION;
  config.timings.v_res = DISPLAY_VERTICAL_RESOLUTION;
  config.timings.hsync_pulse_width = 162;
  config.timings.hsync_back_porch = 152;
  config.timings.hsync_front_porch = 48;
  config.timings.vsync_pulse_width = 45;
  config.timings.vsync_back_porch = 13;
  config.timings.vsync_front_porch = 3;
  config.timings.flags.pclk_active_neg = 1;

  config.data_width = 16;
  config.bits_per_pixel = 16;
  config.num_fbs = 1;
  config.bounce_buffer_size_px = DISPLAY_HORIZONTAL_RESOLUTION * 10; // avoids screen drift while PSRAM is busy
  config.dma_burst_size = 64;

  config.hsync_gpio_num = 46;
  config.vsync_gpio_num = 3;
  config.de_gpio_num = 5;
  config.pclk_gpio_num = 7;
  config.disp_gpio_num = -1;

  const int dataPins[16] = {14, 38, 18, 17, 10, 39, 0, 45, 48, 47, 21, 1, 2, 42, 41, 40}; // B3-B7, G2-G7, R3-R7
  for (int i = 0; i < 16; i++)
  {
    config.data_gpio_nums[i] = dataPins[i];
  }

  config.flags.fb_in_psram = 1;

  esp_err_t error = esp_lcd_new_rgb_panel(&config, &handle);
  if (error != ESP_OK)
  {
    LOG_PRINTF("esp_lcd_new_rgb_panel failed: %s\n", esp_err_to_name(error));
    return false;
  }

  error = esp_lcd_panel_init(handle);
  if (error != ESP_OK)
  {
    LOG_PRINTF("esp_lcd_panel_init failed: %s\n", esp_err_to_name(error));
    return false;
  }

  return true;
}

void RgbPanel::draw(int xStart, int yStart, int xEnd, int yEnd, const void *pixels)
{
  if (handle != nullptr)
  {
    esp_lcd_panel_draw_bitmap(handle, xStart, yStart, xEnd, yEnd, pixels);
  }
}
