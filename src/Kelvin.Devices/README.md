# Kelvin.Devices

Firmware and supporting software for the physical Kelvin devices, including sensor nodes, gateways, and the kiosk experience.

## Directory layout

- `Common/` shared packet definitions used by both gateway and node firmware.
- `Gateway/` ESP32 gateway firmware that receives ESP-NOW packets and forwards framed packets over serial to Kelvin.Server.
- `Node/` ESP32S3 Power Feather sensor node firmware that samples environment data and transmits updates through the gateway; see the [Node README](Node/README.md) for details.
- `Kiosk/` Raspberry Pi python kiosk application; see the [Kiosk README](Kiosk/README.md) for details.

## Notes

- Node and Gateway are designed to use the same payload contract in `Common/SensorPayload.h`.
- Serial framing from the gateway is used by Kelvin.Server device ingestion.
