#pragma once

// EspNow Configuration
#define GATEWAY_MAC_ADDRESS_BYTES {0x00, 0x00, 0x00, 0x00, 0x00, 0x00} // replace with your gateway MAC address

// EnvironmentMonitor's shouldSendUpdate() is shared with Node, so it reuses the same knobs even though this
// panel is always-on (no deep sleep): TIMER_WAKE_INTERVAL_S is really "how often we poll the sensor in
// loop()", HEARTBEAT_INTERVAL_S is "send a reading even if nothing changed" - must stay a whole multiple.
#define TIMER_WAKE_INTERVAL_S 60ULL
#define HEARTBEAT_INTERVAL_S 300ULL

// This board (ESP32-S3-Touch-LCD-7B) uses an "IO EXTENSION" helper chip for backlight,
// touch reset, LCD reset, SD card CS, and battery ADC - confirmed via Waveshare's official Arduino demo
// source (github.com/waveshareteam/ESP32-S3-Touch-LCD-7B, examples/06_LCD and 08_TOUCH: io_extension.h/
// .cpp) to be a single-I2C-address, register-mapped chip (Mode=0x02, Output=0x03, Input=0x04, PWM=0x05,
// ADC=0x06 - NOT CH422G's multi-address protocol, which only applies to the plain "-7" board). This
// confirms the ORIGINAL "CH32V003-like helper MCU" identification was right; an earlier session's
// "correction" to CH422G/0x20 was based on the wrong (non-B) board's library profile - don't repeat that
// mistake. `Hmi/src/io/IoExtension.h` implements the Mode/Output registers (backlight, touch reset);
// BatteryMonitor.cpp implements the ADC register directly - both share this same address.
#define IO_EXPANDER_SDA_PIN 8
#define IO_EXPANDER_SCL_PIN 9
#define IO_EXPANDER_I2C_ADDRESS 0x24

// Rough single-cell LiPo discharge range used to turn the CH422G's raw ADC voltage into a percentage -
// there's no real fuel gauge here (unlike Node's PowerFeather), just a linear approximation.
#define BATTERY_EMPTY_VOLTAGE 3.2f
#define BATTERY_FULL_VOLTAGE 4.2f

// The SHT40 is wired to the board's I2C port, so it shares the IO_EXPANDER_SDA_PIN/SCL_PIN bus above
// (address 0x44, set by EnvironmentMonitor::begin) - Hmi.ino passes that same Wire to the monitor.

// Confirmed via Waveshare's official ESP32-S3-Touch-LCD-7B Arduino demo (rgb_lcd_port.h: EXAMPLE_LCD_H_RES/
// V_RES) - 1024x600, matching the original product-description guess. `esp_panel_board_custom_conf.h` in
// this sketch folder carries the matching RGB timing/pin configuration - these two values MUST keep
// matching what's configured there.
#define DISPLAY_HORIZONTAL_RESOLUTION 1024
#define DISPLAY_VERTICAL_RESOLUTION 600

// uncomment to print debug messages to serial
#define DEBUG 1
