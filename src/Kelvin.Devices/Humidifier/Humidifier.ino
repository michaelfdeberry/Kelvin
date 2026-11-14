#include <Arduino.h>
#include <HumidifierProtocol.h>
#include <Communication/Communicator.h>
#include <Logger.h>
#include "Config.h"
#include "./src/protocol/HumidifierMessages.h"

// Always powered (USB); the solenoid's 24V AC still only exists while the furnace heats. The server pushes the
// relay state through the Gateway and the device acknowledges each one; otherwise it checks in periodically.

static Communicator communicator;
static const uint8_t gatewayMac[6] = GATEWAY_MAC_ADDRESS_BYTES;

static bool relayOn = false;
static bool communicatorReady = false;
static unsigned long lastBeginAttemptAt = 0;
static bool hasCheckedIn = false;
static unsigned long lastCheckInAt = 0;
static unsigned long lastStateAt = 0;

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

static bool sendRelayState(HumidifierMessageType type)
{
  uint8_t buffer[32];
  size_t length = HumidifierMessages::encodeRelayState(buffer, sizeof(buffer), type, relayOn);
  if (length > 0 && communicator.send(buffer, length))
    return true;

  LOG_PRINTF("Failed to send message type %u\n", (unsigned)type);
  return false;
}

static void handleStates()
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

    lastStateAt = millis();
    setRelay(enabled);
    sendRelayState(HumidifierMessageType::StateAck);
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

  if (!hasCheckedIn || millis() - lastCheckInAt >= CHECK_IN_INTERVAL_MS)
  {
    bool sent = sendRelayState(HumidifierMessageType::CheckIn);
    // A check-in the Gateway didn't acknowledge is retried after COMMUNICATOR_RETRY_MS instead of a full interval.
    lastCheckInAt = sent ? millis() : millis() - CHECK_IN_INTERVAL_MS + COMMUNICATOR_RETRY_MS;
    hasCheckedIn = true;
  }

  handleStates();

  // Never leave the solenoid open without recent state from the server.
  if (relayOn && millis() - lastStateAt >= STATE_TIMEOUT_MS)
  {
    LOG_PRINTLN("No state from the server, releasing the relay");
    setRelay(false);
  }

  delay(50);
}
