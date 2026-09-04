#pragma once

// Misc
#define CONTEXT_BUTTON_PIN 15

// Battery Configuration
#define BATTERY_CAPACITY_MAH 1000
#define BATTERY_CHARGING_CURRENT_MA 100

// Display Configuration
#define DISPLAY_SCLK_PIN 39
#define DISPLAY_MOSI_PIN 40
#define DISPLAY_DC_PIN 45
#define DISPLAY_CS_PIN 12
#define DISPLAY_RST_PIN 18
#define DISPLAY_BL_PIN 11

// EspNow Configuration
#define GATEWAY_MAC_ADDRESS_BYTES {0x30, 0x76, 0xF5, 0xF7, 0x0B, 0xB8}
// #define GATEWAY_MAC_ADDRESS_BYTES {0x00, 0x00, 0x00, 0x00, 0x00, 0x00} // replace with your gateway MAC address

// uncomment to print debug messages to serial
#define DEBUG 1
