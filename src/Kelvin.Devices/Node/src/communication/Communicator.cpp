#include <Arduino.h>
#include <esp_now.h>
#include <stdio.h>
#include <string.h>
#include <WiFi.h>
#include <Wire.h>
#include "../Common/SensorPayload.h"
#include "Communicator.h"

void Communicator::begin()
{
#if defined(DEBUG)
  Serial.println("Initializing ESP-NOW...");
#endif

  WiFi.mode(WIFI_STA);
  WiFi.disconnect();

  if (esp_now_init() != ESP_OK)
  {
#if defined(DEBUG)
    Serial.println("Error initializing ESP-NOW");
#endif
    return;
  }

  static const uint8_t gatewayMac[] = GATEWAY_MAC_ADDRESS_BYTES;
  memcpy(gatewayMacAddress, gatewayMac, sizeof(gatewayMacAddress));

  esp_now_peer_info_t peerInfo = {};
  memcpy(peerInfo.peer_addr, gatewayMacAddress, sizeof(gatewayMacAddress));
  peerInfo.channel = 0;
  peerInfo.encrypt = false;

  if (esp_now_add_peer(&peerInfo) != ESP_OK)
  {
#if defined(DEBUG)
    Serial.println("Failed to add peer");
#endif
    return;
  }
}

bool Communicator::send(const void *payload)
{
  size_t size = sizeof(sensor_payload);
  esp_err_t result = esp_now_send(gatewayMacAddress, (uint8_t *)payload, size);

  if (result != ESP_OK)
  {
#if defined(DEBUG)
    Serial.println("Radio transmission failed");
#endif
    return false;
  }
  return true;
}