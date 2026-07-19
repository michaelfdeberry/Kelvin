#pragma once

// Sensors Types
#define ENV_SENSOR_DHT11 1
#define ENV_SENSOR_SCD4X 2
#define ENV_SENSOR_SHT41 3
#define ENV_SENSOR_TYPE ENV_SENSOR_SCD4X

// DHT11 Configuration
#define DHT11_PIN 14

// Battery Configuration
#define BATTERY_PIN A0
#define BATTERY_REFERENCE_VOLTAGE 3.3
#define BATTERY_ADC_RESOLUTION 4095
#define BATTERY_MULTIPLICATION_FACTOR 2

// EspNow Configuration
#define GATEWAY_MAC_ADDRESS "00:00:00:00:00:00"; // replace with Gateway MAC address