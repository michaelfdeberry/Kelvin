#include <Arduino.h>
#include <esp_now.h>
#include <stdio.h>
#include <string.h>
#include <WiFi.h>
#include "Communicator.h"
#include "../Logger.h"

#define SEND_TIMEOUT_MS 50UL
#define FRAME_TAG_SIZE 4

static volatile bool txDone = false;
static volatile bool txSucceeded = false;

// Written by the ESP-NOW recv callback, drained by tryReceive() - see Communicator.h for why a single
// slot (rather than a queue) is sufficient here.
static uint8_t expectedFrameTag[FRAME_TAG_SIZE] = {0};
static uint8_t receiveBuffer[Communicator::MAX_FRAME_CONTENT];
static volatile size_t receiveLength = 0;
static volatile bool receivePending = false;

static void onDataSent(const esp_now_send_info_t *info, esp_now_send_status_t status)
{
  txSucceeded = status == ESP_NOW_SEND_SUCCESS;
  txDone = true;
}

static void onDataRecv(const esp_now_recv_info *info, const uint8_t *incomingData, int len)
{
  if (len < FRAME_TAG_SIZE || memcmp(incomingData, expectedFrameTag, FRAME_TAG_SIZE) != 0)
  {
    return; // not tagged for this device's protocol - ignore (radio noise or an unrelated frame)
  }

  size_t contentLength = min((size_t)(len - FRAME_TAG_SIZE), Communicator::MAX_FRAME_CONTENT);
  memcpy(receiveBuffer, incomingData + FRAME_TAG_SIZE, contentLength);
  receiveLength = contentLength;
  receivePending = true;
}

void Communicator::begin(const uint8_t gatewayMac[6], const uint8_t frameTag[4])
{
  memcpy(gatewayMacAddress, gatewayMac, sizeof(gatewayMacAddress));
  memcpy(expectedFrameTag, frameTag, sizeof(expectedFrameTag));

  LOG_PRINTLN("Initializing ESP-NOW...");

  WiFi.mode(WIFI_STA);
  WiFi.disconnect();

  if (esp_now_init() != ESP_OK)
  {
    LOG_PRINTLN("Error initializing ESP-NOW");
    return;
  }

  esp_now_register_send_cb(onDataSent);
  esp_now_register_recv_cb(onDataRecv);

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

bool Communicator::send(const void *payload, size_t payloadLength)
{
  if (payloadLength > MAX_FRAME_CONTENT)
  {
    LOG_PRINTLN("Payload too large to send");
    return false;
  }

  uint8_t frame[FRAME_TAG_SIZE + MAX_FRAME_CONTENT];
  memcpy(frame, expectedFrameTag, FRAME_TAG_SIZE);
  memcpy(frame + FRAME_TAG_SIZE, payload, payloadLength);

  txDone = false;
  txSucceeded = false;

  esp_err_t result = esp_now_send(gatewayMacAddress, frame, FRAME_TAG_SIZE + payloadLength);

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

bool Communicator::tryReceive(uint8_t *buffer, size_t bufferSize, size_t &outLength)
{
  if (!receivePending)
  {
    return false;
  }

  size_t availableLength = receiveLength; // copy out of volatile storage before comparing
  outLength = min(availableLength, bufferSize);
  memcpy(buffer, receiveBuffer, outLength);
  receivePending = false;
  return true;
}

void Communicator::end()
{
  esp_now_deinit();
  WiFi.mode(WIFI_OFF);
}