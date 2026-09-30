#include <esp_now.h>
#include <WiFi.h>
#include <string.h>
#include "../Common/SensorPayload.h"

// Every ESP-NOW frame is tagged so the sender/frame-type is identified explicitly rather than inferred from
// length alone: Node readings (nodeFrameTag, see Common/SensorPayload.h) and Hmi messages (hmiFrameTag below -
// readings and commands alike, no distinction made here). Node readings are relayed with a fixed-size header
// the server can decode directly. Hmi messages are relayed generically, keyed by the sender's MAC address -
// this gateway never needs to understand what's inside them (including telling a reading apart from a
// command), only the server does.
sensor_payload incomingReadings;

const uint8_t packetHeader[2] = {0xAA, 0x55};
const uint8_t infoHeader[2] = {0xAB, 0x56};
const uint8_t deviceUplinkHeader[2] = {0xAC, 0x57};
const uint8_t deviceDownlinkHeader[2] = {0xAD, 0x58};
const size_t MAX_DEVICE_PAYLOAD = 240;

// Tags every Hmi radio frame so it can be positively identified rather than assumed to be "whatever isn't a
// sensor reading".
const uint8_t hmiFrameTag[4] = {0x4B, 0x48, 0x4D, 0x49}; // "KHMI"

void OnDataRecv(const esp_now_recv_info *info, const uint8_t *incomingData, int len)
{
  if (len >= (int)sizeof(hmiFrameTag) && memcmp(incomingData, hmiFrameTag, sizeof(hmiFrameTag)) == 0)
  {
    size_t payloadLength = min((size_t)len - sizeof(hmiFrameTag), MAX_DEVICE_PAYLOAD);
    uint16_t payloadLengthLE = (uint16_t)payloadLength;

    Serial.write(deviceUplinkHeader, sizeof(deviceUplinkHeader));
    Serial.write(info->src_addr, 6);
    Serial.write(reinterpret_cast<uint8_t *>(&payloadLengthLE), sizeof(payloadLengthLE));
    Serial.write(incomingData + sizeof(hmiFrameTag), payloadLength);
    return;
  }

  if (len != (int)(sizeof(nodeFrameTag) + sizeof(sensor_payload)) || memcmp(incomingData, nodeFrameTag, sizeof(nodeFrameTag)) != 0)
  {
    // Anything else (wrong size/tag) is discarded - radio noise or an unrelated ESP-NOW sender.
    return;
  }

  memcpy(&incomingReadings, incomingData + sizeof(nodeFrameTag), sizeof(incomingReadings));

  Serial.write(packetHeader, sizeof(packetHeader));
  Serial.write(info->src_addr, 6);
  Serial.write(reinterpret_cast<uint8_t *>(&incomingReadings), sizeof(incomingReadings));
}

void setup()
{
  Serial.begin(9600);
  while (!Serial)
  {
    delay(100);
  }

  WiFi.mode(WIFI_STA);
  WiFi.disconnect();

  if (esp_now_init() != ESP_OK)
  {
    Serial.println("{\"error\":\"Critical: Error initializing ESP-NOW\"}");
    return;
  }

  esp_now_register_recv_cb(esp_now_recv_cb_t(OnDataRecv));
}

// A downlink target only needs to be added once; esp_now_is_peer_exist tracks that for us.
bool ensurePeer(const uint8_t *macAddress)
{
  if (esp_now_is_peer_exist(macAddress))
  {
    return true;
  }

  esp_now_peer_info_t peerInfo = {};
  memcpy(peerInfo.peer_addr, macAddress, 6);
  peerInfo.channel = 0;
  peerInfo.encrypt = false;

  return esp_now_add_peer(&peerInfo) == ESP_OK;
}

// Blocks (up to Serial's configured timeout) until `length` bytes have been read, or fails.
bool readSerialBytes(uint8_t *buffer, size_t length)
{
  size_t total = 0;
  while (total < length)
  {
    size_t n = Serial.readBytes(buffer + total, length - total);
    if (n == 0)
    {
      return false;
    }
    total += n;
  }
  return true;
}

// Reads a [MAC][2-byte LE length][payload] frame from the server and relays it to that device over ESP-NOW.
void handleDeviceDownlink()
{
  uint8_t macAddress[6];
  if (!readSerialBytes(macAddress, sizeof(macAddress)))
  {
    return;
  }

  uint16_t payloadLength = 0;
  if (!readSerialBytes(reinterpret_cast<uint8_t *>(&payloadLength), sizeof(payloadLength)))
  {
    return;
  }

  if (payloadLength == 0 || payloadLength > MAX_DEVICE_PAYLOAD)
  {
    return;
  }

  // The frame tag is a radio-only framing concern, so it's added here rather than by the server.
  uint8_t frame[sizeof(hmiFrameTag) + MAX_DEVICE_PAYLOAD];
  memcpy(frame, hmiFrameTag, sizeof(hmiFrameTag));
  if (!readSerialBytes(frame + sizeof(hmiFrameTag), payloadLength))
  {
    return;
  }

  if (ensurePeer(macAddress))
  {
    esp_now_send(macAddress, frame, sizeof(hmiFrameTag) + payloadLength);
  }
}

void loop()
{
  if (Serial.available() > 0)
  {
    int first = Serial.peek();

    if (first == deviceDownlinkHeader[0])
    {
      Serial.read(); // consume the byte we just peeked

      // The second header byte should already be in the buffer right behind the first; this just guards
      // against the rare case it hasn't arrived yet at the moment we check.
      int second = -1;
      unsigned long headerWaitStart = millis();
      while (second < 0 && millis() - headerWaitStart < 50)
      {
        second = Serial.read();
      }

      if (second == deviceDownlinkHeader[1])
      {
        handleDeviceDownlink();
      }
    }
    else
    {
      String command = Serial.readStringUntil('\n');
      command.trim();

      if (command.equalsIgnoreCase("info"))
      {
        uint8_t macAddress[6];
        WiFi.macAddress(macAddress);
        delay(200);

        Serial.write(infoHeader, sizeof(infoHeader));
        Serial.write(macAddress, sizeof(macAddress));
      }
    }
  }

  delay(100);
}