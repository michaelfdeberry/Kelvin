#include <esp_now.h>
#include <WiFi.h>
#include "../Common/SensorPayload.h"

sensor_payload incomingReadings;

void printMacAddress()
{
  uint8_t mac[6];
  WiFi.macAddress(mac);
  char macStr[18];
  snprintf(macStr, sizeof(macStr), "%02x:%02x:%02x:%02x:%02x:%02x", mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
  Serial.print(macStr);
}

// Callback function executed automatically whenever an ESP-NOW packet is received
// void OnDataRecv(const uint8_t *mac, const uint8_t *incomingData, int len)
void OnDataRecv(const esp_now_recv_info *info, const uint8_t *incomingData, int len)
{
  memcpy(&incomingReadings, incomingData, sizeof(incomingReadings));

  const uint8_t header[2] = {0xAA, 0x55};
  Serial.write(header, sizeof(header));
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
      Serial.print("Gateway MAC: ");
      printMacAddress();
      Serial.println();
    }
  }

  delay(100);
}