#include <Arduino.h>
#include <DHT.h>
#include "Config.h"
#include "DHT11Sensor.h"
#include "../Common/SensorPayload.h"

int pin = DHT11_PIN;
DHT dht(pin, DHT11);

void DHT11Sensor::begin()
{
  dht.begin();
}

bool DHT11Sensor::read(sensor_payload &payload)
{
  float humidity = dht.readHumidity();
  float temperature = dht.readTemperature();

  if (isnan(humidity) || isnan(temperature))
  {
#if defined(DEBUG)
    Serial.println("Error: Failed to read from DHT sensor! Check wiring/timing.");
#endif
    return false;
  }

  payload.humidity = humidity;
  payload.temperature = temperature;
  payload.co2 = 0;

  return true;
}
