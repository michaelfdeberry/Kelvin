#include <Arduino.h>
#include <LovyanGFX.hpp>
#include "Config.h"
#include "Display.h"
#include "../Common/SensorPayload.h"
#include "../battery/BatteryMonitor.h"

// -------------------------------------------------------------------------
// LGFX HARDWARE CONFIGURATION
// -------------------------------------------------------------------------
LGFX::LGFX(void)
{
  auto b_cfg = _bus_instance.config();
  b_cfg.spi_host = VSPI_HOST;
  b_cfg.spi_mode = 0;
  b_cfg.freq_write = 40000000;
  b_cfg.freq_read = 16000000;
  b_cfg.spi_3wire = true;
  b_cfg.use_lock = true;
  b_cfg.dma_channel = SPI_DMA_CH_AUTO;
  b_cfg.pin_sclk = 18;
  b_cfg.pin_mosi = 23;
  b_cfg.pin_miso = -1;
  b_cfg.pin_dc = 2;
  _bus_instance.config(b_cfg);
  _panel_instance.setBus(&_bus_instance);

  auto p_cfg = _panel_instance.config();
  p_cfg.pin_cs = 15;
  p_cfg.pin_rst = 4;
  p_cfg.pin_busy = -1;
  p_cfg.memory_width = 240;
  p_cfg.memory_height = 320;
  p_cfg.panel_width = 240;
  p_cfg.panel_height = 320;
  p_cfg.offset_x = 0;
  p_cfg.offset_y = 0;
  p_cfg.invert = true;
  p_cfg.rgb_order = false;
  _panel_instance.config(p_cfg);

  auto l_cfg = _light_instance.config();
  l_cfg.pin_bl = 32; // When connected directly to 3.3V use -1
  l_cfg.invert = false;
  l_cfg.freq = 44100;
  l_cfg.pwm_channel = 7;
  _light_instance.config(l_cfg);
  _panel_instance.setLight(&_light_instance);

  setPanel(&_panel_instance);
}

// -------------------------------------------------------------------------
// Display IMPLEMENTATION
// -------------------------------------------------------------------------
Display::Display(unsigned long timeoutMs)
    : isAwake(false),
      lastActivityTime(0),
      timeoutMs(timeoutMs),
      lastTemp(-999.0),
      lastHum(-999.0),
      lastBatteryLevel(-1),
      showFahrenheit(false)
#if ENV_SENSOR_TYPE == ENV_SENSOR_SCD4X
      ,
      _lastCo2(0)
#endif
{
  buttonPin = CONTEXT_BUTTON_PIN;
}

void Display::begin()
{
  prefs.begin("kelvin", true);
  showFahrenheit = prefs.getBool("tempF", false);
  prefs.end();

  tft.init();
  tft.setRotation(1);
  tft.fillScreen(THEME_BG);
  sleep();
}

void Display::wakeUp()
{
  lastActivityTime = millis();

  if (!isAwake)
  {

    tft.wakeup();
    tft.setBrightness(255);
    tft.fillScreen(THEME_BG);

    isAwake = true;
    lastTemp = -999.0;
    lastHum = -999.0;
    lastBatteryLevel = -1;
    lastMac = "";
#if ENV_SENSOR_TYPE == ENV_SENSOR_SCD4X
    lastCo2 = 0;
#endif
  }
}

void Display::sleep()
{
  if (isAwake)
  {
    tft.setBrightness(0);
    tft.sleep();
    isAwake = false;
  }
}

bool Display::awake() const
{
  return isAwake;
}

void Display::toggleTempUnit()
{
  showFahrenheit = !showFahrenheit;
  prefs.begin("kelvin", false); // read-write
  prefs.putBool("tempF", showFahrenheit);
  prefs.end();
}

void Display::checkSleepTimeout()
{
  // Auto-sleep if timeout exceeded
  if (isAwake && (millis() - lastActivityTime > timeoutMs))
  {
    sleep();
  }
}

void Display::tick(volatile bool &buttonPressed, unsigned long &lastInterruptTime, const String &macAddress, const sensor_payload &payload)
{
  if (buttonPressed)
  {
    buttonPressed = false;
    if (millis() - lastInterruptTime > 200)
    {
      lastInterruptTime = millis();
      const unsigned long LONG_PRESS_MS = 600;
      unsigned long pressStart = millis();
      while (digitalRead(buttonPin) == LOW && millis() - pressStart < LONG_PRESS_MS + 100)
      {
        delay(10);
      }
      if (millis() - pressStart >= LONG_PRESS_MS)
      {
        toggleTempUnit();
      }
      wakeUp();
    }
  }

  checkSleepTimeout();
  if (awake())
  {
    updateDisplay(macAddress, payload);
  }
}

void Display::updateDisplay(const String &macAddress, const sensor_payload &payload)
{
  // Do not attempt to draw to the screen if the display controller is sleeping
  if (!isAwake)
    return;

  int currentBattery = payload.batteryLevel;

  tft.startWrite();

  // 1. Update MAC Address (Top)
  if (macAddress != lastMac)
  {
    tft.setTextPadding(tft.textWidth(lastMac, &fonts::Font2));
    tft.setTextDatum(TC_DATUM);
    tft.setTextColor(THEME_MUTED, THEME_BG);
    tft.drawString(macAddress, tft.width() / 2, 15, &fonts::Font2);

    lastMac = macAddress;
    tft.setTextPadding(0);
  }

  // 2. Update Low Battery Icon (Top Right)
  if (currentBattery != lastBatteryLevel)
  {
    if (currentBattery <= 20)
    {
      drawBatteryIcon(currentBattery);
    }
    else if (lastBatteryLevel <= 20 && currentBattery > 20)
    {
      clearBatteryIcon();
    }
    lastBatteryLevel = currentBattery;
  }

  // 3. Update Temperature (Center)
  float displayTemp = showFahrenheit ? (payload.temperature * 9.0f / 5.0f + 32.0f) : payload.temperature;
  const char *unitLetter = showFahrenheit ? "F" : "C";
  if (abs(displayTemp - lastTemp) > 0.05)
  {
    String numStr = String(displayTemp, 1);
    int numWidth = tft.textWidth(numStr, &fonts::Font6);
    int oldNumWidth = tft.textWidth(String(lastTemp, 1), &fonts::Font6);
    int unitWidth = tft.textWidth(unitLetter, &fonts::Font4);
    const int spacing = 8;
    const int circleDiameter = 10;
    int suffixWidth = spacing + circleDiameter + spacing + unitWidth;

    int numFontHeight = tft.fontHeight(&fonts::Font6);
    int unitFontHeight = tft.fontHeight(&fonts::Font4);
    int centerX = tft.width() / 2;
    int centerY = tft.height() / 2;

    // Clear the widest of the old/new footprint before redrawing
    int clearWidth = max(numWidth, oldNumWidth) + suffixWidth;
    tft.fillRect(centerX - clearWidth / 2 - 2, centerY - numFontHeight / 2 - 2, clearWidth + 4, numFontHeight + 4, THEME_BG);

    tft.setTextDatum(ML_DATUM);
    tft.setTextColor(THEME_TEXT, THEME_BG);

    int x = centerX - (numWidth + suffixWidth) / 2;
    x += tft.drawString(numStr, x, centerY, &fonts::Font6);

    x += spacing;
    int circleY = centerY - unitFontHeight / 2 + circleDiameter / 2;
    tft.drawCircle(x + circleDiameter / 2, circleY, circleDiameter / 2, THEME_TEXT);
    x += circleDiameter + spacing;

    tft.drawString(unitLetter, x, centerY, &fonts::Font4);

    lastTemp = displayTemp;
  }

  // 4. Update Humidity / CO2 (Bottom)
#if ENV_SENSOR_TYPE == ENV_SENSOR_SCD4X
  if (abs(payload.humidity - lastHum) > 0.05 || payload.co2 != lastCo2)
  {
    String bottomStr = String(payload.humidity, 1) + "% RH  -  " + String(payload.co2) + "ppm CO2";

    tft.setTextPadding(tft.textWidth(String(bottomStr, 1), &fonts::Font4) + 10);
    tft.setTextDatum(BC_DATUM);
    tft.setTextColor(THEME_MUTED, THEME_BG);
    tft.drawString(bottomStr, tft.width() / 2, tft.height() - 20, &fonts::Font4);

    lastHum = payload.humidity;
    lastCo2 = payload.co2;
    tft.setTextPadding(0);
  }
#else
  if (abs(payload.humidity - lastHum) > 0.05)
  {
    String humStr = String(payload.humidity, 1) + "% RH";

    tft.setTextPadding(tft.textWidth(String(lastHum, 1) + "% RH", &fonts::Font4) + 10);
    tft.setTextDatum(BC_DATUM);
    tft.setTextColor(THEME_MUTED, THEME_BG);
    tft.drawString(humStr, tft.width() / 2, tft.height() - 20, &fonts::Font4);

    lastHum = payload.humidity;
    tft.setTextPadding(0);
  }
#endif

  tft.endWrite();
}

void Display::drawBatteryIcon(int level)
{
  int x = tft.width() - 35;
  int y = 15;

  tft.drawRect(x, y, 24, 12, THEME_ALERT);
  tft.fillRect(x + 24, y + 3, 3, 6, THEME_ALERT);

  int fillWidth = (20 * level) / 100;
  if (fillWidth < 1)
    fillWidth = 1;

  tft.fillRect(x + 2, y + 2, fillWidth, 8, THEME_ALERT);
  tft.fillRect(x + 2 + fillWidth, y + 2, 20 - fillWidth, 8, THEME_BG);
}

void Display::clearBatteryIcon()
{
  int x = tft.width() - 35;
  int y = 15;
  tft.fillRect(x, y, 28, 12, THEME_BG);
}