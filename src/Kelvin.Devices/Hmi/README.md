# Kelvin Hmi Panel

Firmware for the Kelvin wall-mounted thermostat panel: a 7" touch display that mirrors the Node's onboard
environment reading and adds full thermostat control (mode, set points, schedules, forecast lockouts) sent
to a Kelvin gateway over ESP-NOW, using the same wire protocol documented in `Common/src/HmiProtocol.h`.

## Status: structure and interactivity are stubbed

This sketch is a starting skeleton, not a finished UI:

- ESP-NOW communication (sending readings/commands, receiving thermostat state pushes) is fully implemented.
- The onboard environment sensor path reuses `Common`'s `EnvironmentMonitor` (assumes an SHT4x, matching
  Node - update `src/battery/BatteryMonitor` and `EnvironmentMonitor`'s bus config if the real hardware
  differs).
- LVGL is initialized, but the actual RGB display panel driver and GT911 touch driver are **not wired up
  yet** - `src/ui/Ui.cpp`'s `flushCallback`/`touchReadCallback` are placeholders (they compile, but don't
  draw or read touch input). See the TODOs there and in `Config.h`.
- `Ui`'s `onModeSelected`/`onFanToggled`/`onSetPointAdjusted` already build and send the real wire messages -
  wiring a real widget's event callback to one of these is the only remaining step once the UI is designed.
- Battery: read via the CH32V003 helper MCU's ADC register over I2C (address `0x24`, pins confirmed - see
  `Config.h`) - there's no real fuel gauge, so the percentage is a linear approximation over a typical
  single-cell LiPo voltage range. See `src/battery/BatteryMonitor.cpp`.

## Board

Waveshare ESP32-S3 7" Touch Display Development Board (Type B), 1024x600 IPS, RGB parallel LCD bus + CH422G
IO expander + GT911 capacitive touch (per Waveshare's product description - pin numbers are **not yet
verified** against the wiki/schematic, see `Config.h`).

Arduino CLI fully qualified board name (generic ESP32-S3 with octal PSRAM, needed for the 1024x600
framebuffer):

```text
esp32:esp32:esp32s3:PSRAM=opi
```

## Setup

Install the required Arduino libraries and LVGL config from the Hmi directory:

```powershell
.\Hmi.bat
```

Compile from the repository root. Unlike Node/Gateway, this sketch depends on the shared `Common` library,
so `--libraries` must point at its parent folder:

```powershell
arduino-cli compile --fqbn "esp32:esp32:esp32s3:PSRAM=opi" --libraries "src/Kelvin.Devices" "src/Kelvin.Devices/Hmi"
```

## Directory layout

- `Hmi.ino` - setup/loop: reads the sensor, drains downlink messages, ticks LVGL.
- `Config.h` - gateway MAC, sensor bus pins, display resolution - several values are unverified placeholders
  (marked TODO).
- `src/battery/` - battery voltage/percentage via the CH32V003 helper MCU's I2C ADC register.
- `src/protocol/` - `HmiCommandEncoder` (builds outgoing command bytes) and `HmiStateDecoder` (reassembles
  chunked thermostat state pushes from the server).
- `src/ui/` - LVGL init + stubbed display/touch drivers + stubbed interaction handlers.
- `../Common/src/HmiProtocol.h` - the wire message types and byte layouts, mirrored from Kelvin.Server's
  `Models/HmiMessageType.cs`/`Models/FrameTags.cs`/`Features/Hmi/HmiThermostatStateEncoder.cs`. Keep them in
  sync if either side changes.

## Shared code

`Communicator` (ESP-NOW send/receive) and `EnvironmentMonitor` (SHT4x reading + change/heartbeat filtering)
now live in `../Common` alongside Node, since both devices need the same ESP-NOW framing and the same onboard
sensor. `Common` is a proper Arduino library (`library.properties` + `src/`) rather than a plain include
folder, specifically so its `.cpp` files get compiled once and linked into whichever sketch depends on it -
that's why compiling either Node or Hmi now requires the extra `--libraries "src/Kelvin.Devices"` flag.
