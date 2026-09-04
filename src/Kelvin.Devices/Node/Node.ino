#include <Arduino.h>
#include <WiFi.h>
#include <Wire.h>
#include <PowerFeather.h>
#include <esp_sleep.h>
#include <driver/rtc_io.h>
#include "../Common/SensorPayload.h"
#include "./src/communication/Communicator.h"
#include "./src/environment/EnvironmentMonitor.h"
#include "./src/display/Display.h"
#include "Config.h"
#include "Logger.h"

using namespace PowerFeather;

Communicator communicator;
EnvironmentMonitor environmentMonitor;
Display display;
String macAddress;

volatile bool buttonPressedFlag = false;

#define SDA_PIN 47
#define SCL_PIN 48

#define TIMER_WAKE_INTERVAL_US (30ULL * 1000000ULL)
#define DISPLAY_AWAKE_MS 30000UL

// The ISR function (keep this as short as possible)
void IRAM_ATTR handleButtonInterrupt()
{
  buttonPressedFlag = true;
}

// Re-arms timer/button wake sources and powers down; never returns.
void goToSleep()
{
  communicator.end();
  Board.enableVSQT(false);

  esp_sleep_enable_timer_wakeup(TIMER_WAKE_INTERVAL_US);
  esp_sleep_enable_ext0_wakeup((gpio_num_t)CONTEXT_BUTTON_PIN, 0);
  // RTC pad floats during sleep unless the pull-up is set on the RTC domain itself
  rtc_gpio_pullup_en((gpio_num_t)CONTEXT_BUTTON_PIN);
  rtc_gpio_pulldown_dis((gpio_num_t)CONTEXT_BUTTON_PIN);

  esp_deep_sleep_start();
}

void setup()
{
  LOG_BEGIN(9600);

  esp_sleep_wakeup_cause_t wakeCause = esp_sleep_get_wakeup_cause();
  bool wokeByButton = wakeCause == ESP_SLEEP_WAKEUP_EXT0 || wakeCause == ESP_SLEEP_WAKEUP_UNDEFINED;

  Wire.begin(SDA_PIN, SCL_PIN);
  pinMode(CONTEXT_BUTTON_PIN, INPUT_PULLUP);

  Board.init(BATTERY_CAPACITY_MAH);
  Board.enableVSQT(true);

  communicator.begin();
  environmentMonitor.begin();
  macAddress = WiFi.macAddress();

  sensor_payload payload;
  if (!environmentMonitor.read(payload))
  {
    LOG_PRINTLN("Failed to read sensor data.");
    goToSleep();
  }

  if (wokeByButton)
  {
    display.begin();
    buttonPressedFlag = true; // the wake itself counts as the initial press
    attachInterrupt(digitalPinToInterrupt(CONTEXT_BUTTON_PIN), handleButtonInterrupt, FALLING);

    unsigned long lastInterruptTime = 0;
    unsigned long awakeUntil = millis() + DISPLAY_AWAKE_MS;
    while ((long)(awakeUntil - millis()) > 0)
    {
      display.tick(buttonPressedFlag, lastInterruptTime, macAddress, payload);
      delay(display.awake() ? 100 : 1000);
    }

    detachInterrupt(digitalPinToInterrupt(CONTEXT_BUTTON_PIN));
    display.sleep();
  }

  if (environmentMonitor.shouldSendUpdate(payload))
  {
    if (communicator.send(&payload))
    {
      LOG_PRINT("Sensor data sent successfully from ");
      LOG_PRINTLN(macAddress);
    }
    else
    {
      LOG_PRINTLN("Failed to send sensor data.");
    }
  }

  goToSleep();
}

void loop()
{
  // never reached: setup() ends every wake cycle in deep sleep
}
