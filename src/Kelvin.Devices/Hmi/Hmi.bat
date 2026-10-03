arduino-cli lib install "Sensirion Core"
arduino-cli lib install "Sensirion I2C SHT4x"
arduino-cli lib install "lvgl@9.2.2"
REM Official Waveshare board-support stack for the RGB panel/GT911 touch/CH422G backlight (see
REM esp_panel_board_custom_conf.h for this board's confirmed pin/timing values).
arduino-cli lib install "ESP32_Display_Panel"

REM LVGL needs lv_conf.h next to the lvgl library folder (LVGL's own Arduino convention), enabled by
REM flipping the "#if 0" at the top of the template to "#if 1". This has to be done once per machine.
REM The Ui also needs a few extra Montserrat sizes beyond the template's default 14-only (20 for section
REM labels/buttons, 32 for the onboard sensor card/set point values, 48 for the big current-temperature
REM readout) - flipped on here too so a fresh machine doesn't silently get the wrong fonts.
powershell -NoProfile -Command "$t = Get-Content \"$env:USERPROFILE\Documents\Arduino\libraries\lvgl\lv_conf_template.h\" -Raw; $t = $t -replace '(?m)^#if 0 /\*Set it to \"1\" to enable content\*/', '#if 1 /*Set it to \"1\" to enable content*/'; $t = $t -replace '(?m)^#define LV_FONT_MONTSERRAT_20 0$', '#define LV_FONT_MONTSERRAT_20 1'; $t = $t -replace '(?m)^#define LV_FONT_MONTSERRAT_32 0$', '#define LV_FONT_MONTSERRAT_32 1'; $t = $t -replace '(?m)^#define LV_FONT_MONTSERRAT_48 0$', '#define LV_FONT_MONTSERRAT_48 1'; Set-Content -Path \"$env:USERPROFILE\Documents\Arduino\libraries\lv_conf.h\" -Value $t -NoNewline"
