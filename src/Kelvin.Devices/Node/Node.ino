#include <Arduino.h>
#include <WiFi.h>
#include <Wire.h>
#include <PowerFeather.h>
#include <esp_mac.h>
#include <esp_sleep.h>
#include <driver/rtc_io.h>
#include <SensorPayload.h>
#include <Communication/Communicator.h>
#include <Environment/EnvironmentMonitor.h>
#include "./src/battery/BatteryMonitor.h"
#include "./src/display/Display.h"
#include "Config.h"
#include <Logger.h>

using namespace PowerFeather;

Communicator communicator;
EnvironmentMonitor environmentMonitor;
BatteryMonitor batteryMonitor;
Display display;
String macAddress;
bool radioStarted = false;

volatile bool buttonPressedFlag = false;
volatile bool powerButtonPressedFlag = false;
unsigned long powerButtonPressStarted = 0;

#define TIMER_WAKE_INTERVAL_US (TIMER_WAKE_INTERVAL_S * 1000000ULL)
#define TIMER_WAKE_INTERVAL_MS ((unsigned long)(TIMER_WAKE_INTERVAL_S * 1000ULL))
#define DISPLAY_AWAKE_MS 30000UL
#define POWER_STATE_POLL_MS 1000UL

// Reads the factory MAC without bringing up the WiFi stack, so the radio can stay off.
String readMacAddress()
{
  uint8_t mac[6] = {0};
  esp_read_mac(mac, ESP_MAC_WIFI_STA);

  char formatted[18];
  snprintf(formatted, sizeof(formatted), "%02X:%02X:%02X:%02X:%02X:%02X", mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
  return String(formatted);
}

// The ISR function (keep this as short as possible)
void IRAM_ATTR handleButtonInterrupt()
{
  buttonPressedFlag = true;
}

void IRAM_ATTR handlePowerButtonInterrupt()
{
  powerButtonPressedFlag = true;
}

bool powerButtonHeldLongEnough()
{
  if (!powerButtonPressedFlag)
  {
    powerButtonPressStarted = 0;
    return false;
  }

  if (digitalRead(POWERFEATHER_BUTTON_PIN) == HIGH)
  {
    powerButtonPressedFlag = false;
    powerButtonPressStarted = 0;
    return false;
  }

  if (powerButtonPressStarted == 0)
  {
    powerButtonPressStarted = millis();
  }

  while (digitalRead(POWERFEATHER_BUTTON_PIN) == LOW && millis() - powerButtonPressStarted < POWERFEATHER_BUTTON_HOLD_MS)
  {
    delay(10);
  }

  if (digitalRead(POWERFEATHER_BUTTON_PIN) == HIGH)
  {
    powerButtonPressedFlag = false;
    powerButtonPressStarted = 0;
    return false;
  }

  return true;
}

// Re-arms timer/button wake sources and powers down; never returns.
void goToSleep()
{
  if (powerButtonHeldLongEnough())
  {
    detachInterrupt(digitalPinToInterrupt(POWERFEATHER_BUTTON_PIN));
    batteryMonitor.enterShutdownMode();
  }

  if (radioStarted)
  {
    communicator.end();
  }

  Board.enableVSQT(false);
  Board.enable3V3(false);
  digitalWrite(LED, LOW);

  LOG_FLUSH();

  esp_sleep_enable_timer_wakeup(TIMER_WAKE_INTERVAL_US);
  esp_sleep_enable_ext0_wakeup((gpio_num_t)CONTEXT_BUTTON_PIN, 0);
  // RTC pad floats during sleep unless the pull-up is set on the RTC domain itself
  rtc_gpio_pullup_en((gpio_num_t)CONTEXT_BUTTON_PIN);
  rtc_gpio_pulldown_dis((gpio_num_t)CONTEXT_BUTTON_PIN);

  esp_deep_sleep_start();
}

// Evaluates a fresh reading against the change/heartbeat rules and transmits when needed.
void sendIfNeeded(const sensor_payload &payload)
{
  if (!environmentMonitor.shouldSendUpdate(payload))
  {
    return;
  }

  if (!radioStarted)
  {
    static const uint8_t gatewayMac[] = GATEWAY_MAC_ADDRESS_BYTES;
    communicator.begin(gatewayMac, nodeFrameTag);
    radioStarted = true;
  }

  if (communicator.send(&payload, sizeof(payload)))
  {
    LOG_PRINTLN("Sensor data sent successfully.");
  }
  else
  {
    LOG_PRINTLN("Failed to send sensor data.");
  }
}

// Runs the screen until it times out; on external power it stays on indefinitely,
// re-reading the sensor on the normal wake cadence instead of deep sleeping.
void runDisplaySession(sensor_payload &payload, battery_status &battery)
{
  macAddress = readMacAddress();

  Board.enable3V3(true);
  delay(25);

  display.begin();
  delay(25);

  buttonPressedFlag = true; // the wake counts as the initial press
  attachInterrupt(digitalPinToInterrupt(CONTEXT_BUTTON_PIN), handleButtonInterrupt, FALLING);

  unsigned long lastInterruptTime = 0;
  unsigned long lastReadTime = millis();
  unsigned long lastPowerPollTime = millis();
  unsigned long awakeUntil = millis() + DISPLAY_AWAKE_MS;

  while (battery.externalPower || (long)(awakeUntil - millis()) > 0)
  {
    if (powerButtonHeldLongEnough())
    {
      detachInterrupt(digitalPinToInterrupt(CONTEXT_BUTTON_PIN));
      detachInterrupt(digitalPinToInterrupt(POWERFEATHER_BUTTON_PIN));
      batteryMonitor.enterShutdownMode();
    }

    if (millis() - lastReadTime >= TIMER_WAKE_INTERVAL_MS)
    {
      lastReadTime = millis();
      if (environmentMonitor.read(payload))
      {
        battery = batteryMonitor.getStatus();
        payload.batteryLevel = battery.level;
        sendIfNeeded(payload);
      }
    }
    else if (millis() - lastPowerPollTime >= POWER_STATE_POLL_MS)
    {
      // Notice plug/unplug quickly without a full sensor read
      lastPowerPollTime = millis();
      battery = batteryMonitor.getStatus();
    }

    display.tick(buttonPressedFlag, lastInterruptTime, macAddress, payload, battery);

    // Another press re-wakes the board through ext0, so there's no reason to idle here with
    // the 3V3 rail up once the screen has timed out.
    if (!battery.externalPower && !display.awake())
    {
      break;
    }

    delay(100);
  }

  detachInterrupt(digitalPinToInterrupt(CONTEXT_BUTTON_PIN));
  detachInterrupt(digitalPinToInterrupt(POWERFEATHER_BUTTON_PIN));
  display.sleep();

  delay(100);
  Board.enable3V3(false);
}

void setup()
{
  esp_sleep_wakeup_cause_t wakeCause = esp_sleep_get_wakeup_cause();
  bool coldBoot = wakeCause == ESP_SLEEP_WAKEUP_UNDEFINED;
  bool wokeByButton = wakeCause == ESP_SLEEP_WAKEUP_EXT0 || coldBoot;

  setCpuFrequencyMhz(CPU_FREQUENCY_MHZ);

  LOG_BEGIN(9600, coldBoot);

  pinMode(CONTEXT_BUTTON_PIN, INPUT_PULLUP);
  pinMode(POWERFEATHER_BUTTON_PIN, INPUT_PULLUP);
  attachInterrupt(digitalPinToInterrupt(POWERFEATHER_BUTTON_PIN), handlePowerButtonInterrupt, FALLING);

#if defined(DEBUG_WAKE_LED)
  // turn on the light to show the device is awake
  pinMode(LED, OUTPUT);
  digitalWrite(LED, HIGH);
#endif

  Board.init(BATTERY_CAPACITY_MAH, Mainboard::BatteryType::Generic_3V7);
  Board.enableVSQT(true);

  // wait for the Stemma V rail to stabilize before initializing components
  delay(25);

  environmentMonitor.begin(Wire1);
  batteryMonitor.begin();

  // wait for the components to stabilize before reading sensor data
  delay(25);

  sensor_payload payload;
  if (!environmentMonitor.read(payload))
  {
    LOG_PRINTLN("Failed to read sensor data.");
    goToSleep();
  }

  battery_status battery = batteryMonitor.getStatus();
  payload.batteryLevel = battery.level;

  if (powerButtonHeldLongEnough())
  {
    detachInterrupt(digitalPinToInterrupt(POWERFEATHER_BUTTON_PIN));
    batteryMonitor.enterShutdownMode();
  }

  sendIfNeeded(payload);

  if (wokeByButton || battery.externalPower)
  {
    runDisplaySession(payload, battery);
  }

  goToSleep();
}

void loop()
{
  // never reached: setup() ends every wake cycle in deep sleep
}
