#pragma once

// Misc
#define CONTEXT_BUTTON_PIN 15
#define POWERFEATHER_BUTTON_PIN 0
#define POWERFEATHER_BUTTON_HOLD_MS 3000UL

// Battery Configuration
#define BATTERY_CAPACITY_MAH 1000
#define BATTERY_CHARGING_CURRENT_MA 500

// Display Configuration
#define DISPLAY_SCLK_PIN 16
#define DISPLAY_MOSI_PIN 6
#define DISPLAY_DC_PIN 17
#define DISPLAY_CS_PIN 12
#define DISPLAY_RST_PIN 18
#define DISPLAY_BL_PIN 11

// EspNow Configuration
// #define GATEWAY_MAC_ADDRESS_BYTES {0x00, 0x00, 0x00, 0x00, 0x00, 0x00} // replace with your gateway MAC address
#define GATEWAY_MAC_ADDRESS_BYTES {0x30, 0x76, 0xF5, 0xF7, 0x0B, 0xB8}

// Wake cadence for the periodic sensor read. Longer intervals cut the fixed per-wake
// boot/radio overhead proportionally, which dominates this device's battery budget.
#define TIMER_WAKE_INTERVAL_S 60ULL

// How often a reading is sent even when nothing changed enough to trigger an update.
// Must stay a whole multiple of the wake interval; the server and gateway assume 5 minutes.
#define HEARTBEAT_INTERVAL_S 300ULL

// The workload is I2C and radio waits rather than compute, so the lowest WiFi-capable clock
// roughly halves active current. Valid values: 80, 160, 240.
#define CPU_FREQUENCY_MHZ 80

// uncomment to print debug messages to serial
#define DEBUG 1

// uncomment to light the user LED for the whole awake window (costs ~1-5 mA while lit)
// #define DEBUG_WAKE_LED 1
