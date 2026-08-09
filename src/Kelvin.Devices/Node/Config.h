#pragma once

// Sensors Types
#define ENV_SENSOR_DHT11 1
#define ENV_SENSOR_SCD4X 2
#define ENV_SENSOR_SHT4X 3
#define ENV_SENSOR_TYPE ENV_SENSOR_SHT4X

// DHT11 Configuration
#define DHT11_PIN 14

// Misc
#define CONTEXT_BUTTON_PIN 33

// Battery Configuration
#define BATTERY_PIN 34
#define BATTERY_MULTIPLICATION_FACTOR 2
#define BATTERY_DEAD_VOLTAGE 3.0
#define BATTERY_CHARGED_VOLTAGE 4.2

// EspNow Configuration
#define GATEWAY_MAC_ADDRESS_BYTES {0x00, 0x00, 0x00, 0x00, 0x00, 0x00} // replace with your gateway MAC address

// uncomment to print debug messages to serial
// #define DEBUG 1
