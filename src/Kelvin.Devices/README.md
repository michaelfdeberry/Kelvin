# Kelvin.Devices

Firmware for the physical Kelvin devices.

## Directory layout

- `Gateway/` ESP32 gateway firmware that receives ESP-NOW packets and forwards framed packets over serial to Kelvin.Server.
- `Node/` ESP32 sensor node firmware that samples environment data and transmits updates through the gateway.
- `Common/` shared packet definitions used by both gateway and node firmware.

## Notes

- Node and Gateway are designed to use the same payload contract in `Common/SensorPayload.h`.
- Serial framing from the gateway is used by Kelvin.Server device ingestion.
