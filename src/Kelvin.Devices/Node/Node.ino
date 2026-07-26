#include <Arduino.h>
#include <WiFi.h>
#include <Wire.h>
#include "../Common/SensorPayload.h"
#include "./src/communication/Communicator.h"
#include "./src/environment/EnvironmentMonitor.h"

Communicator communicator;
EnvironmentMonitor environmentMonitor;

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
  communicator.begin();
  environmentMonitor.begin();
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
  Serial.println(WiFi.macAddress());
#endif
  delay(30000);
}
