#include <Arduino.h>
#include <SensirionI2cSht4x.h>
#include <Wire.h>
#include "./SHT4XSensor.h"
#include "../Common/SensorPayload.h"

#ifdef NO_ERROR
#undef NO_ERROR
#endif
#define NO_ERROR 0

static char errorMessage[64];
static int16_t error;

SensirionI2cSht4x sht4x;

void SHT4XSensor::begin()
{
  sht4x.begin(Wire, SHT40_I2C_ADDR_44);
  sht4x.softReset();
}

bool SHT4XSensor::read(sensor_payload &payload)
{
  float temperature = 0.0;
  float relativeHumidity = 0.0;

  error = sht4x.measureHighPrecision(temperature, relativeHumidity);
  if (error != NO_ERROR)
  {
#if defined(DEBUG)
    Serial.print("Error trying to execute measureHighPrecision(): ");
    errorToString(error, errorMessage, sizeof errorMessage);
    Serial.println(errorMessage);
#endif
    return false;
  }

  payload.temperature = temperature;
  payload.humidity = relativeHumidity;
  payload.co2 = 0;
  return true;
}
