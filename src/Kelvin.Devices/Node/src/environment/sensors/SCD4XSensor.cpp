#include <Arduino.h>
#include <SensirionI2cScd4x.h>
#include <Wire.h>
#include "./SCD4XSensor.h"
#include "../Common/SensorPayload.h"

#ifdef NO_ERROR
#undef NO_ERROR
#endif
#define NO_ERROR 0

static char errorMessage[64];
static int16_t error;

SensirionI2cScd4x scd4x;
bool dataReady = false;
uint16_t co2Concentration = 0;
float temperature = 0.0;
float relativeHumidity = 0.0;

void SCD4XSensor::begin()
{
  scd4x.begin(Wire, SCD41_I2C_ADDR_62);
  delay(30);

  // error = scd4x.wakeUp();
  // if (error != NO_ERROR)
  // {
  //   Serial.print("Error trying to execute wakeUp(): ");
  //   errorToString(error, errorMessage, sizeof errorMessage);
  //   Serial.println(errorMessage);
  //   return;
  // }

  error = scd4x.stopPeriodicMeasurement();
  if (error != NO_ERROR)
  {
    Serial.print("Error trying to execute stopPeriodicMeasurement(): ");
    errorToString(error, errorMessage, sizeof errorMessage);
    Serial.println(errorMessage);
    return;
  }

  delay(500);

  // error = scd4x.reinit();
  // if (error != NO_ERROR)
  // {
  //   Serial.print("Error trying to execute reinit(): ");
  //   errorToString(error, errorMessage, sizeof errorMessage);
  //   Serial.println(errorMessage);
  //   return;
  // }

  error = scd4x.startPeriodicMeasurement();
  if (error != NO_ERROR)
  {
    Serial.print("Error trying to execute startPeriodicMeasurement(): ");
    errorToString(error, errorMessage, sizeof errorMessage);
    Serial.println(errorMessage);
    return;
  }
}

bool SCD4XSensor::read(sensor_payload &payload)
{
  error = scd4x.getDataReadyStatus(dataReady);
  if (error != NO_ERROR)
  {
    Serial.print("Error trying to execute getDataReadyStatus(): ");
    errorToString(error, errorMessage, sizeof errorMessage);
    Serial.println(errorMessage);
    return false;
  }

  while (!dataReady)
  {
    delay(100);
    error = scd4x.getDataReadyStatus(dataReady);
    if (error != NO_ERROR)
    {
      Serial.print("Error trying to execute getDataReadyStatus(): ");
      errorToString(error, errorMessage, sizeof errorMessage);
      Serial.println(errorMessage);
      return false;
    }
  }

  error = scd4x.readMeasurement(co2Concentration, temperature, relativeHumidity);
  if (error != NO_ERROR)
  {
    Serial.print("Error trying to execute readMeasurement(): ");
    errorToString(error, errorMessage, sizeof errorMessage);
    Serial.println(errorMessage);
    return false;
  }

  payload.temperature = temperature;
  payload.humidity = relativeHumidity;
  payload.co2 = co2Concentration;
  return true;
}
