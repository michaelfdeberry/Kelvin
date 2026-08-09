#pragma once
#define LGFX_USE_V1

#include <Arduino.h>
#include <LovyanGFX.hpp>
#include <Preferences.h>
#include "Config.h"
#include "../Common/SensorPayload.h"

// --- Environmental Sensor Macros ---
#define ENV_SENSOR_NONE 0
#define ENV_SENSOR_DHT 1
#define ENV_SENSOR_SHT4X 2
#define ENV_SENSOR_SCD4X 3

// Set your active sensor here if not defined globally in your build environment
#ifndef ENV_SENSOR_TYPE
#define ENV_SENSOR_TYPE ENV_SENSOR_SCD4X
#endif

// Custom RGB565 colors
#define THEME_BG 0x18E3    // Dark Navy
#define THEME_TEXT 0xFFFF  // White
#define THEME_MUTED 0x7BEF // Slate/Grey
#define THEME_ALERT 0xF800 // Red

// LovyanGFX Custom Device Configuration for ESP32 + ST7789V (240x320)
class LGFX : public lgfx::LGFX_Device
{
  lgfx::Panel_ST7789 _panel_instance;
  lgfx::Bus_SPI _bus_instance;
  lgfx::Light_PWM _light_instance;

public:
  LGFX(void);
};

class Display
{
private:
  LGFX tft;
  bool isAwake;
  unsigned long lastActivityTime;
  unsigned long timeoutMs;
  String lastMac;
  float lastTemp;
  float lastHum;
  int lastBatteryLevel;
  bool showFahrenheit;
  uint8_t buttonPin;
  Preferences prefs;
#if ENV_SENSOR_TYPE == ENV_SENSOR_SCD4X
  uint16_t lastCo2;
#endif

  void drawBatteryIcon(int level);
  void clearBatteryIcon();

public:
  Display(unsigned long timeoutMs = 5000);

  void begin();

  // Power Management Methods
  void checkSleepTimeout();
  void wakeUp();
  void sleep();
  bool awake() const;

  void toggleTempUnit();
  void tick(volatile bool &buttonPressed, unsigned long &lastInterruptTime, const String &macAddress, const sensor_payload &payload);
  void updateDisplay(const String &macAddress, const sensor_payload &payload);
};