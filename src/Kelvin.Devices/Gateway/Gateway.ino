#include <esp_now.h>
#include <WiFi.h>

// This gateway never inspects a device's frame tag or contents - it just relays every ESP-NOW frame it
// receives to the server as-is (tag included), and relays every frame the server sends back out to the
// addressed device as-is. The server decides what a tag means (see Models/FrameTags.cs), so adding a new
// device type never requires a change here.

const uint8_t infoHeader[2] = {0xAB, 0x56};
const uint8_t deviceUplinkHeader[2] = {0xAC, 0x57};
const uint8_t deviceDownlinkHeader[2] = {0xAD, 0x58};
const size_t MAX_DEVICE_PAYLOAD = 240;

void OnDataRecv(const esp_now_recv_info *info, const uint8_t *incomingData, int len)
{
  if (len <= 0)
  {
    return;
  }

  size_t payloadLength = min((size_t)len, MAX_DEVICE_PAYLOAD);
  uint16_t payloadLengthLE = (uint16_t)payloadLength;

  Serial.write(deviceUplinkHeader, sizeof(deviceUplinkHeader));
  Serial.write(info->src_addr, 6);
  Serial.write(reinterpret_cast<uint8_t *>(&payloadLengthLE), sizeof(payloadLengthLE));
  Serial.write(incomingData, payloadLength);
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

  // The server already includes whatever tag the target device expects in `payload` - this gateway just
  // relays the bytes as given.
  uint8_t payload[MAX_DEVICE_PAYLOAD];
  if (!readSerialBytes(payload, payloadLength))
  {
    return;
  }

  if (ensurePeer(macAddress))
  {
    esp_now_send(macAddress, payload, payloadLength);
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