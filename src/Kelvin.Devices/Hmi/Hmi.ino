#include <Arduino.h>
#include <WiFi.h>
#include <Wire.h>
#include <SensorPayload.h>
#include <HmiProtocol.h>
#include <Communication/Communicator.h>
#include <Environment/EnvironmentMonitor.h>
#include <Logger.h>
#include "Config.h"
#include "./src/battery/BatteryMonitor.h"
#include "./src/protocol/HmiCommandEncoder.h"
#include "./src/protocol/HmiStateDecoder.h"
#include "./src/ui/Ui.h"

Communicator communicator;
EnvironmentMonitor environmentMonitor;
BatteryMonitor batteryMonitor;
HmiStateDecoder stateDecoder;
Ui ui;

unsigned long lastReadTime = 0;

// Drains any downlink message that arrived since the last loop() iteration and routes it by type.
void handleDownlinkMessages()
{
  uint8_t buffer[Communicator::MAX_FRAME_CONTENT];
  size_t length = 0;
  if (!communicator.tryReceive(buffer, sizeof(buffer), length) || length == 0)
  {
    return;
  }

  auto messageType = (HmiMessageType)buffer[0];
  if (messageType == HmiMessageType::ThermostatStateChunk)
  {
    if (stateDecoder.addChunk(buffer + 1, length - 1))
    {
      ui.applyThermostatState(stateDecoder.getState(), stateDecoder.getStateLength());
    }
  }
}

// Reads the onboard sensor and sends a reading when it's changed enough or the heartbeat interval elapsed.
void sendReadingIfNeeded()
{
  sensor_payload payload;
  if (!environmentMonitor.read(payload))
  {
    LOG_PRINTLN("Failed to read sensor data.");
    return;
  }

  battery_status battery = batteryMonitor.getStatus();
  payload.batteryLevel = battery.level;

  if (!environmentMonitor.shouldSendUpdate(payload))
  {
    return;
  }

  uint8_t buffer[Communicator::MAX_FRAME_CONTENT];
  size_t length =
      HmiCommandEncoder::encodeSensorReading(payload.temperature, payload.humidity, payload.batteryLevel, buffer, sizeof(buffer));

  if (length > 0 && communicator.send(buffer, length))
  {
    LOG_PRINTLN("Sensor data sent successfully.");
  }
}

void setup()
{
  LOG_BEGIN(9600, true);

  Wire.begin(IO_EXPANDER_SDA_PIN, IO_EXPANDER_SCL_PIN);

  environmentMonitor.begin(Wire);
  batteryMonitor.begin(Wire);

  static const uint8_t gatewayMac[] = GATEWAY_MAC_ADDRESS_BYTES;
  communicator.begin(gatewayMac, hmiFrameTag);

  ui.begin(communicator);

  LOG_PRINTLN("Hmi ready (interactivity stubbed - see README for remaining hardware bring-up).");
}

void loop()
{
  ui.tick();
  handleDownlinkMessages();

  if (millis() - lastReadTime >= (unsigned long)(TIMER_WAKE_INTERVAL_S * 1000ULL))
  {
    lastReadTime = millis();
    sendReadingIfNeeded();
  }

  delay(5);
}
