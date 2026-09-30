arduino-cli lib install "Sensirion Core"
arduino-cli lib install "Sensirion I2C SHT4x"
arduino-cli lib install "lvgl@9.2.2"

REM LVGL needs lv_conf.h next to the lvgl library folder (LVGL's own Arduino convention), enabled by
REM flipping the "#if 0" at the top of the template to "#if 1". This has to be done once per machine.
powershell -NoProfile -Command "$t = Get-Content \"$env:USERPROFILE\Documents\Arduino\libraries\lvgl\lv_conf_template.h\" -Raw; $t = $t -replace '(?m)^#if 0 /\*Set it to \"1\" to enable content\*/', '#if 1 /*Set it to \"1\" to enable content*/'; Set-Content -Path \"$env:USERPROFILE\Documents\Arduino\libraries\lv_conf.h\" -Value $t -NoNewline"
