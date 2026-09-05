#include <Arduino.h>
#include <esp_now.h>
#include <stdio.h>
#include <string.h>
#include <WiFi.h>
#include "../Common/SensorPayload.h"
#include "Communicator.h"
#include "Logger.h"

#define SEND_TIMEOUT_MS 50UL

static volatile bool txDone = false;
static volatile bool txSucceeded = false;

static void onDataSent(const esp_now_send_info_t *info, esp_now_send_status_t status)
{
  txSucceeded = status == ESP_NOW_SEND_SUCCESS;
  txDone = true;
}

void Communicator::begin()
{
  LOG_PRINTLN("Initializing ESP-NOW...");

  WiFi.mode(WIFI_STA);
  WiFi.disconnect();

  if (esp_now_init() != ESP_OK)
  {
    LOG_PRINTLN("Error initializing ESP-NOW");
    return;
  }

  esp_now_register_send_cb(onDataSent);

  static const uint8_t gatewayMac[] = GATEWAY_MAC_ADDRESS_BYTES;
  memcpy(gatewayMacAddress, gatewayMac, sizeof(gatewayMacAddress));

  esp_now_peer_info_t peerInfo = {};
  memcpy(peerInfo.peer_addr, gatewayMacAddress, sizeof(gatewayMacAddress));
  peerInfo.channel = 0;
  peerInfo.encrypt = false;

  if (esp_now_add_peer(&peerInfo) != ESP_OK)
  {
    LOG_PRINTLN("Failed to add peer");
    return;
  }
}

bool Communicator::send(const void *payload)
{
  size_t size = sizeof(sensor_payload);
  txDone = false;
  txSucceeded = false;

  esp_err_t result = esp_now_send(gatewayMacAddress, (uint8_t *)payload, size);

  if (result != ESP_OK)
  {
    LOG_PRINTLN("Radio transmission failed");
    return false;
  }

  // Blocks until the radio reports the frame was transmitted; deep sleep would otherwise drop it.
  unsigned long start = millis();
  while (!txDone && millis() - start < SEND_TIMEOUT_MS)
  {
    delay(1);
  }

  if (!txDone)
  {
    LOG_PRINTLN("Radio transmission timed out");
    return false;
  }

  return txSucceeded;
}

void Communicator::end()
{
  esp_now_deinit();
  WiFi.mode(WIFI_OFF);
}