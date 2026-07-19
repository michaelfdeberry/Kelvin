#include <Arduino.h>
#include <Wire.h>
#include "../Common/SensorPayload.h"
#include "./src/communication/Communicator.h"
#include "./src/environment/EnvironmentMonitor.h"

Communicator communicator;
EnvironmentMonitor environmentMonitor;

void setup()
{
  Serial.begin(9600);
  Wire.begin();

  communicator.begin();
  environmentMonitor.begin();
}

void printError(const char *message)
{
  Serial.println(message);
  delay(10000);
}

void loop()
{
  sensor_payload payload;
  if (!environmentMonitor.read(payload))
  {
    printError("Failed to read sensor data.");
    return;
  }

  if (!communicator.send(&payload))
  {
    printError("Failed to send sensor data.");
    return;
  }

  Serial.println("Sensor data sent successfully.");
  delay(30000);
}
