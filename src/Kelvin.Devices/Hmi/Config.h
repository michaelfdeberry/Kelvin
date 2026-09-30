#pragma once

// EspNow Configuration
#define GATEWAY_MAC_ADDRESS_BYTES {0x00, 0x00, 0x00, 0x00, 0x00, 0x00} // replace with your gateway MAC address

// EnvironmentMonitor's shouldSendUpdate() is shared with Node, so it reuses the same knobs even though this
// panel is always-on (no deep sleep): TIMER_WAKE_INTERVAL_S is really "how often we poll the sensor in
// loop()", HEARTBEAT_INTERVAL_S is "send a reading even if nothing changed" - must stay a whole multiple.
#define TIMER_WAKE_INTERVAL_S 60ULL
#define HEARTBEAT_INTERVAL_S 300ULL

// Confirmed against ESPHome's merged waveshare_io_ch32v003 component (esphome/esphome#10071) - this board's
// CH32V003 helper MCU (battery ADC, touch reset, backlight PWM) sits on this I2C bus/address. The display
// consumes nearly all other pins, so this is also the most likely bus for any external/onboard sensor.
#define IO_EXPANDER_SDA_PIN 8
#define IO_EXPANDER_SCL_PIN 9
#define IO_EXPANDER_I2C_ADDRESS 0x24

// Rough single-cell LiPo discharge range used to turn the CH32V003's raw ADC voltage into a percentage -
// there's no real fuel gauge here (unlike Node's PowerFeather), just a linear approximation.
#define BATTERY_EMPTY_VOLTAGE 3.2f
#define BATTERY_FULL_VOLTAGE 4.2f

// TODO: an onboard temperature/humidity sensor is not confirmed on this board at all - if/when one is
// wired up, it most likely shares the IO_EXPANDER_SDA_PIN/SCL_PIN bus above (the display leaves little
// room for a second bus); only its I2C address would differ. EnvironmentMonitor::begin() takes whichever
// TwoWire is passed in, so no further plumbing should be needed once that's confirmed.

// TODO: verify against the Waveshare wiki - this board uses an RGB parallel LCD bus plus a CH422G I2C IO
// expander (backlight/reset lines) and a GT911 capacitive touch controller over I2C. None of the pin
// numbers below are confirmed; Ui::begin() stubs the actual panel/touch bring-up until they are.
#define DISPLAY_HORIZONTAL_RESOLUTION 1024
#define DISPLAY_VERTICAL_RESOLUTION 600

// uncomment to print debug messages to serial
// #define DEBUG 1
