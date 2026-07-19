#include <esp_now.h>
#include <stdio.h>
#include <string.h>
#include <WiFi.h>
#include <Wire.h>
#include "../Common/SensorPayload.h"
#include "Communicator.h"

const char *gatewayMacAddress = GATEWAY_MAC_ADDRESS;

std::array<uint8_t, 6> parseGatewayAddress(const char *macAddress)
{
  int octets[6];
  std::array<uint8_t, 6> gatewayAddress = {};
  if (sscanf(macAddress, "%x:%x:%x:%x:%x:%x", &octets[0], &octets[1], &octets[2], &octets[3], &octets[4], &octets[5]) == 6)
  {
    for (int i = 0; i < 6; ++i)
    {
      gatewayAddress[i] = static_cast<uint8_t>(octets[i]);
    }
  }

  return gatewayAddress;
}

// void onDataSent(const uint8_t *mac_addr, esp_now_send_status_t status)
// {
//   Serial.print("ESP-NOW Sent Status: ");
//   Serial.println(status == ESP_NOW_SEND_SUCCESS ? "Success" : "Fail");
// }

void Communicator::begin()
{
  Serial.println("Initializing ESP-NOW...");

  WiFi.mode(WIFI_STA);
  WiFi.disconnect();

  if (esp_now_init() != ESP_OK)
  {
    Serial.println("Error initializing ESP-NOW");
    return;
  }

  // esp_now_register_send_cb((esp_now_send_cb_t)onDataSent);

  esp_now_peer_info_t peerInfo = {};
  auto gatewayAddress = parseGatewayAddress(gatewayMacAddress);
  memcpy(peerInfo.peer_addr, gatewayAddress.data(), gatewayAddress.size());
  peerInfo.channel = 0;
  peerInfo.encrypt = false;

  Serial.print("Connecting to Gateway: ");
  for (int i = 0; i < 6; ++i)
  {
    Serial.printf("%02X", peerInfo.peer_addr[i]);
    if (i < 5)
      Serial.print(":");
  }
  Serial.println();

  if (esp_now_add_peer(&peerInfo) != ESP_OK)
  {
    Serial.println("Failed to add peer");
    return;
  }
}

bool Communicator::send(const void *payload)
{
  size_t size = sizeof(sensor_payload);
  auto gatewayAddress = parseGatewayAddress(gatewayMacAddress);
  esp_err_t result = esp_now_send(gatewayAddress.data(), (uint8_t *)payload, size);

  if (result != ESP_OK)
  {
    Serial.println("Radio transmission failed");
    return false;
  }
  return true;
}