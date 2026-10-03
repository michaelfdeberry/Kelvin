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
#include "./src/protocol/HmiFrameReassembler.h"
#include "./src/protocol/ControlStateParser.h"
#include "./src/protocol/EnvironmentReadingParser.h"
#include "./src/ui/Ui.h"

Communicator communicator;
EnvironmentMonitor environmentMonitor;
BatteryMonitor batteryMonitor;
HmiFrameReassembler frameReassembler;
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

  if (!frameReassembler.addFrame(buffer, length))
  {
    return; // either a non-final chunk, or a malformed/truncated frame - nothing to dispatch yet
  }

  auto messageType = frameReassembler.getType();
  const uint8_t *message = frameReassembler.getMessage();
  size_t messageLength = frameReassembler.getMessageLength();

  if (messageType == HmiMessageType::ThermostatStateChunk)
  {
    ui.applyThermostatState(message, messageLength);
  }
  else if (messageType == HmiMessageType::ControlStateChanged)
  {
    ControlCallState controlState;
    if (ControlStateParser::parse(message, messageLength, controlState))
    {
      ui.applyControlState(controlState);
    }
    else
    {
      LOG_PRINTLN("Failed to parse a ControlStateChanged message (truncated/malformed) - ignoring.");
    }
  }
  else if (messageType == HmiMessageType::EnvironmentReadingChanged)
  {
    EnvironmentAverageReading reading;
    if (EnvironmentReadingParser::parse(message, messageLength, reading))
    {
      ui.applyEnvironmentAverage(reading);
    }
    else
    {
      LOG_PRINTLN("Failed to parse an EnvironmentReadingChanged message (truncated/malformed) - ignoring.");
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

  // The UI reflects every fresh reading immediately, independent of the send-throttling below.
  ui.applyEnvironmentReading(payload.temperature, payload.humidity);

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

  // Must run before ui.begin(): the display panel library (CH422G/GT911) shares this exact bus and is
  // configured to skip its own host init, reusing whatever Wire.begin() already set up here (see
  // esp_panel_board_custom_conf.h's SKIP_INIT_HOST comments) - installing the I2C driver twice on the
  // same port fails.
  Wire.begin(IO_EXPANDER_SDA_PIN, IO_EXPANDER_SCL_PIN, 400000);

  environmentMonitor.begin(Wire);
  batteryMonitor.begin(Wire);

  static const uint8_t gatewayMac[] = GATEWAY_MAC_ADDRESS_BYTES;
  communicator.begin(gatewayMac, hmiFrameTag);

  ui.begin(communicator);

  LOG_PRINTLN("Hmi ready");
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
