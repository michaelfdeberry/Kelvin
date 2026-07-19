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
  // The Adafruit library returns floats instead of ints
  float humidity = dht.readHumidity();
  // Read temperature as Celsius (default).
  // Pass true to readTemperature(true) if you want Fahrenheit.
  float temperature = dht.readTemperature();

  // The library returns NAN (Not a Number) if the microsecond timing fails
  if (isnan(humidity) || isnan(temperature))
  {
    Serial.println("Error: Failed to read from DHT sensor! Check wiring/timing.");
    return false;
  }

  // Assign to payload (these will implicitly cast to int if your struct requires it)
  payload.humidity = humidity;
  payload.temperature = temperature;

  return true;
}
