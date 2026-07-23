#include <esp_now.h>
#include <WiFi.h>
#include "../Common/SensorPayload.h"

sensor_payload incomingReadings;

const uint8_t packetHeader[2] = {0xAA, 0x55};
const uint8_t infoHeader[2] = {0xAB, 0x56};

void OnDataRecv(const esp_now_recv_info *info, const uint8_t *incomingData, int len)
{
  memcpy(&incomingReadings, incomingData, sizeof(incomingReadings));

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

void loop()
{
  if (Serial.available() > 0)
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

  delay(100);
}