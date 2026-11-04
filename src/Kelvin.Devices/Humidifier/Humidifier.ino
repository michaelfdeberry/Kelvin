#include <Arduino.h>
#include <HumidifierProtocol.h>
#include <Communication/Communicator.h>
#include <Logger.h>
#include "Config.h"
#include "./src/protocol/HumidifierMessages.h"

// Powered from the humidifier's own transformer, so this only runs while the furnace is heating. It pings the
// server through the Gateway and drives the solenoid relay from the reply.

static Communicator communicator;
static const uint8_t gatewayMac[6] = GATEWAY_MAC_ADDRESS_BYTES;

static bool relayOn = false;
static bool communicatorReady = false;
static unsigned long lastBeginAttemptAt = 0;
static bool hasPinged = false;
static unsigned long lastPingAt = 0;
static unsigned long lastResponseAt = 0;

static void writeRelay(bool on)
{
  digitalWrite(RELAY_PIN, on == (RELAY_ACTIVE_HIGH != 0) ? HIGH : LOW);
}

static void setRelay(bool on)
{
  if (on == relayOn)
    return;

  relayOn = on;
  writeRelay(on);
  LOG_PRINTF("Humidifier relay %s\n", on ? "on" : "off");
}

static void sendPing()
{
  uint8_t buffer[16];
  size_t length = HumidifierMessages::encodePing(buffer, sizeof(buffer), relayOn);
  if (length == 0 || !communicator.send(buffer, length))
    LOG_PRINTLN("Failed to send ping");
}

static void handleResponses()
{
  uint8_t buffer[Communicator::MAX_FRAME_CONTENT];
  size_t length;
  while (communicator.tryReceive(buffer, sizeof(buffer), length))
  {
    bool enabled;
    if (!HumidifierMessages::decodeState(buffer, length, enabled))
    {
      LOG_PRINTLN("Ignoring an invalid humidifier frame");
      continue;
    }

    lastResponseAt = millis();
    setRelay(enabled);
  }
}

void setup()
{
  // Set the off level before enabling the output so the solenoid never pulses on boot.
  writeRelay(false);
  pinMode(RELAY_PIN, OUTPUT);

  LOG_BEGIN(115200, true);
  LOG_PRINTLN("Humidifier booting");

  communicatorReady = communicator.begin(gatewayMac, humidifierFrameTag);
  lastBeginAttemptAt = millis();
}

void loop()
{
  if (!communicatorReady)
  {
    if (millis() - lastBeginAttemptAt >= COMMUNICATOR_RETRY_MS)
    {
      LOG_PRINTLN("Retrying ESP-NOW startup");
      communicatorReady = communicator.begin(gatewayMac, humidifierFrameTag);
      lastBeginAttemptAt = millis();
    }
    delay(50);
    return;
  }

  if (!hasPinged || millis() - lastPingAt >= PING_INTERVAL_MS)
  {
    sendPing();
    lastPingAt = millis();
    hasPinged = true;
  }

  handleResponses();

  // Never leave the solenoid open without a recent answer from the server.
  if (relayOn && millis() - lastResponseAt >= RESPONSE_TIMEOUT_MS)
  {
    LOG_PRINTLN("No response from the server, releasing the relay");
    setRelay(false);
  }

  delay(50);
}
