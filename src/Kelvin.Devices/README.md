# Kelvin.Devices

Firmware and supporting software for the physical Kelvin devices, including sensor nodes, gateways, and the kiosk experience.

## Directory layout

- `Common/` shared Arduino library (ESP-NOW communication, environment sensing, logging, and the packet/Hmi wire-protocol contracts) used by the Node and Hmi firmware. It's a real Arduino library (`library.properties` + `src/`), not a plain include folder - compiling Node or Hmi needs `--libraries "src/Kelvin.Devices"` so its source files are found and linked.
- `Gateway/` ESP32 gateway firmware that relays ESP-NOW frames to/from Kelvin.Server over serial, generically by tag - it never needs to change when a new device type is added.
- `Node/` ESP32S3 Power Feather sensor node firmware that samples environment data and transmits updates through the gateway; see the [Node README](Node/README.md) for details.
- `Hmi/` ESP32-S3 touch panel firmware (wall-mounted thermostat control);see the [Hmi README](Hmi/README.md).
- `Kiosk/` Raspberry Pi python kiosk application; see the [Kiosk README](Kiosk/README.md) for details.

## Notes

- Node and Hmi share their ESP-NOW/sensor code via the `Common` library; each device tags its own frames (see `Common/src/SensorPayload.h` and `Common/src/HmiProtocol.h`) so the server can identify the sender without the Gateway needing to know about it.
- Serial framing from the gateway is used by Kelvin.Server device ingestion.
