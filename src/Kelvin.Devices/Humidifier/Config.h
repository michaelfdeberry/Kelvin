#pragma once

// EspNow Configuration
#define GATEWAY_MAC_ADDRESS_BYTES {0x00, 0x00, 0x00, 0x00, 0x00, 0x00} // replace with your gateway MAC address

// Relay module input driving the humidifier solenoid.
#define RELAY_PIN 26
#define RELAY_ACTIVE_HIGH 1

// How often the server is asked whether the humidifier should run.
#define PING_INTERVAL_MS 15000UL

// Release the relay if the server hasn't answered for this long.
#define RESPONSE_TIMEOUT_MS 120000UL

// How long to wait before retrying a failed ESP-NOW startup.
#define COMMUNICATOR_RETRY_MS 5000UL

// No sensor on this device.
#define DISABLE_ENVIRONMENT_MONITOR 1

// uncomment to print debug messages to serial
// #define DEBUG 1
