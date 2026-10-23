#include <Arduino.h>
#include <WiFi.h>
#include <Wire.h>
#include <SensorPayload.h>
#include <HmiProtocol.h>
#include <Communication/Communicator.h>
#include <Environment/EnvironmentMonitor.h>
#include <Logger.h>
#include <Preferences.h>
#include "Config.h"
#include "./src/battery/BatteryMonitor.h"
#include "./src/protocol/HmiCommandEncoder.h"
#include "./src/protocol/HmiFrameReassembler.h"
#include "./src/protocol/ControlStateParser.h"
#include "./src/protocol/EnvironmentReadingParser.h"
#include "./src/protocol/PreferencesParser.h"
#include "./src/ui/Ui.h"

Communicator communicator;
EnvironmentMonitor environmentMonitor;
BatteryMonitor batteryMonitor;
HmiFrameReassembler frameReassembler;
Ui ui;
Preferences prefs;

unsigned long lastReadTime = 0;
unsigned long lastStateRequestTime = 0;
bool controlStateReceived = false;
bool preferencesReceived = false;
bool thermostatStateReceived = false;
const unsigned long STATE_REQUEST_RETRY_MS = 5000;

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
    if (ui.applyThermostatState(message, messageLength))
    {
      thermostatStateReceived = true;
    }
  }
  else if (messageType == HmiMessageType::ControlStateChanged)
  {
    ControlCallState controlState;
    if (ControlStateParser::parse(message, messageLength, controlState))
    {
      ui.applyControlState(controlState);
      controlStateReceived = true;
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
  else if (messageType == HmiMessageType::PreferencesChanged)
  {
    SystemPreferences preferences;
    if (PreferencesParser::parse(message, messageLength, preferences))
    {
      bool temperatureFahrenheit = preferences.temperatureUnit == TemperatureUnit::Fahrenheit;
      bool hour12 = preferences.timeFormat == TimeFormat::Hour12;
      prefs.begin("system", false);
      bool temperatureChanged = prefs.getBool("tempF", !temperatureFahrenheit) != temperatureFahrenheit;
      bool timeFormatChanged = prefs.getBool("hour12", !hour12) != hour12;
      if (temperatureChanged)
      {
        prefs.putBool("tempF", temperatureFahrenheit);
      }
      if (timeFormatChanged)
      {
        prefs.putBool("hour12", hour12);
      }
      prefs.end();
      if (temperatureChanged || timeFormatChanged)
      {
        ui.applyPreferences();
      }
      preferencesReceived = true;
    }
    else
    {
      LOG_PRINTLN("Failed to parse a PreferencesChanged message (truncated/malformed) - ignoring.");
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
  ui.applyBattery(battery.level, battery.externalPower);

  // The UI reflects every fresh reading immediately, independent of the send-throttling below.
  ui.applyEnvironmentReading(payload.temperature, payload.humidity);

  if (!environmentMonitor.shouldSendUpdate(payload))
  {
    return;
  }

  uint8_t buffer[Communicator::MAX_FRAME_CONTENT];
  size_t length = HmiCommandEncoder::encodeSensorReading(payload.temperature, payload.humidity, payload.batteryLevel, buffer, sizeof(buffer));

  if (length > 0 && communicator.send(buffer, length))
  {
    LOG_PRINTLN("Sensor data sent successfully.");
  }
}

// The server's reply is lost if the gateway isn't connected yet, so keep asking until a state arrives.
void requestStateIfNeeded()
{
  if ((controlStateReceived && preferencesReceived && thermostatStateReceived) ||
      (lastStateRequestTime != 0 && millis() - lastStateRequestTime < STATE_REQUEST_RETRY_MS))
  {
    return;
  }

  lastStateRequestTime = millis();
  uint8_t buffer[Communicator::MAX_FRAME_CONTENT];
  size_t length = HmiCommandEncoder::encodeRequestState(buffer, sizeof(buffer));
  if (length > 0)
  {
    communicator.send(buffer, length);
  }
}

void setup()
{
  LOG_BEGIN(115200, false);
  LOG_PRINTLN("Hmi booting");

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
  if (!ui.isScreenOff())
  {
    requestStateIfNeeded();
  }

  if (lastReadTime == 0 || millis() - lastReadTime >= (unsigned long)(TIMER_WAKE_INTERVAL_S * 1000ULL))
  {
    lastReadTime = millis();
    sendReadingIfNeeded();
  }

  if (ui.isScreenOff())
  {
    unsigned long interval = (unsigned long)(TIMER_WAKE_INTERVAL_S * 1000ULL);
    unsigned long elapsed = millis() - lastReadTime;
    if (elapsed < interval)
    {
      communicator.end();
      bool wokeByTouch = ui.sleepFor((uint64_t)(interval - elapsed) * 1000ULL);
      static const uint8_t gatewayMac[] = GATEWAY_MAC_ADDRESS_BYTES;
      communicator.begin(gatewayMac, hmiFrameTag);
      if (wokeByTouch)
      {
        ui.wakeScreen();
        controlStateReceived = false;
        preferencesReceived = false;
        thermostatStateReceived = false;
        lastStateRequestTime = 0;
        lastReadTime = 0;
        requestStateIfNeeded();
      }
    }
  }

  delay(5);
}
