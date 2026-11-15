#pragma once

// EspNow Configuration
#define GATEWAY_MAC_ADDRESS_BYTES {0x00, 0x00, 0x00, 0x00, 0x00, 0x00} // replace with your gateway MAC address

// Relay module input driving the humidifier solenoid.
#define RELAY_PIN 26
#define RELAY_ACTIVE_HIGH 1

// How often the device checks in with the server (it also checks in at boot).
#define CHECK_IN_INTERVAL_MS 300000UL

// Release the relay if the server hasn't sent a state for this long; it resends every 30 seconds while running.
#define STATE_TIMEOUT_MS 120000UL

// How long to wait before retrying a failed ESP-NOW startup.
#define COMMUNICATOR_RETRY_MS 5000UL

// No sensor on this device.
#define DISABLE_ENVIRONMENT_MONITOR 1

// uncomment to print debug messages to serial
// #define DEBUG 1
