#include <Arduino.h>
#include <WiFi.h>
#include <Wire.h>
#include "../Common/SensorPayload.h"
#include "./src/communication/Communicator.h"
#include "./src/environment/EnvironmentMonitor.h"
#include "./src/display/Display.h"

Communicator communicator;
EnvironmentMonitor environmentMonitor;
Display display;
String macAddress;

unsigned long lastUpdate = 0;

volatile bool buttonPressedFlag = true;

// The ISR function (keep this as short as possible)
void IRAM_ATTR handleButtonInterrupt()
{
  buttonPressedFlag = true;
}

void setup()
{
#if defined(DEBUG)
  Serial.begin(9600);
  while (!Serial)
  {
    delay(100);
  }
#endif

  Wire.begin();

  pinMode(CONTEXT_BUTTON_PIN, INPUT_PULLUP);
  attachInterrupt(digitalPinToInterrupt(CONTEXT_BUTTON_PIN), handleButtonInterrupt, FALLING);

  environmentMonitor.begin();
  communicator.begin();
  display.begin();

  macAddress = WiFi.macAddress();
}

void loop()
{
  sensor_payload payload;
  if (!environmentMonitor.read(payload))
  {
#if defined(DEBUG)
    Serial.println("Failed to read sensor data.");
#endif

    delay(10000);
    return;
  }

  if (environmentMonitor.shouldSendUpdate(payload))
  {
    if (!communicator.send(&payload))
    {
#if defined(DEBUG)
      Serial.println("Failed to send sensor data.");
#endif

      delay(10000);
      return;
    }

#if defined(DEBUG)
    Serial.print("Sensor data sent successfully from ");
    Serial.println(macAddress);
#endif
  }
  else
  {
#if defined(DEBUG)
    Serial.println("No significant change in sensor data. Skipping send.");
#endif
  }

  static unsigned long lastInterruptTime = 0;
  unsigned long waitUntil = millis() + 30000;
  while ((long)(waitUntil - millis()) > 0)
  {
    unsigned long remaining = waitUntil - millis();
    unsigned long sleepMs = display.awake() ? 100 : min(remaining, 1000UL);

    display.tick(buttonPressedFlag, lastInterruptTime, macAddress, payload);
    delay(sleepMs);
  }
}
