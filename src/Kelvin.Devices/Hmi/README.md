# Kelvin Hmi Panel

Firmware for the Kelvin wall-mounted thermostat panel: a 7" touch display that mirrors the Node's onboard
environment reading and adds full thermostat control (mode, set points, schedules, forecast lockouts) sent
to a Kelvin gateway over ESP-NOW, using the same wire protocol documented in `Common/src/HmiProtocol.h`.

## Board

Waveshare **ESP32-S3-Touch-LCD-7B** (the "B" revision - not the plain "-7"; the two look similar but have
different resolutions/timings/IO-expander chips, see below). The esp32 Arduino core has no dedicated board
definition for the "7B" specifically, so this sketch uses the generic `waveshare_esp32_s3_touch_lcd_7` FQBN
(same ESP32-S3 module/pinout/flash/PSRAM); the panel timing is configured in `src/display/RgbPanel.cpp`.

1024x600 IPS RGB-parallel LCD (Waveshare's docs call the chip "ST7701", but the official demo code drives
it with zero init commands, so it is driven as a plain RGB panel via `esp_lcd`) + a register-mapped "IO EXTENSION" helper chip
(I2C address `0x24`, driven by `src/io/IoExtension.h`) for backlight/touch-reset + GT911 capacitive touch
over I2C. Battery monitoring uses a separate Adafruit LC709203F fuel gauge at `0x0B`. All display
pin/timing/register values are confirmed via Waveshare's official Arduino demo source
(`github.com/waveshareteam/ESP32-S3-Touch-LCD-7B`, `examples/Arduino/examples/16_LVGL_V9_DEMO`), not
guessed. The ESP32_Display_Panel library is deliberately not used: its I2C code uses the legacy ESP-IDF I2C
driver, which aborts at boot when linked alongside `Wire` on arduino-esp32 3.3.x.

Arduino CLI fully qualified board name (PSRAM is on-board and required for the framebuffer; the "Huge APP"
partition scheme is needed because the UI's LVGL widget tree plus the display panel library already use a
large share of the default partition's app space):

```text
esp32:esp32:waveshare_esp32_s3_touch_lcd_7:PSRAM=enabled,PartitionScheme=huge_app
```

## Setup

Install the required Arduino libraries and LVGL config from the Hmi directory:

```powershell
.\Hmi.bat
```

This installs `Adafruit LC709203F` and its `Adafruit BusIO` dependency along with the sensor and UI libraries.

Compile from the repository root.

```powershell
arduino-cli compile --fqbn "esp32:esp32:waveshare_esp32_s3_touch_lcd_7:PSRAM=enabled,PartitionScheme=huge_app" --libraries "src/Kelvin.Devices" "src/Kelvin.Devices/Hmi"
```

Upload from the repository root.

```powershell
arduino-cli compile --fqbn "esp32:esp32:waveshare_esp32_s3_touch_lcd_7:PSRAM=enabled,PartitionScheme=huge_app" --libraries "src/Kelvin.Devices" --upload -p COMX "src/Kelvin.Devices/Hmi"
```

Replace `COMX` with the board's actual serial port.

## Message reassembly

HMI messages use a 9-byte envelope: `[type:1][chunk index:1][chunk count:1][body length:2 LE][message ID:4 LE]`,
followed by the body. Every chunk of one message shares the same ID. The panel retains one assembly for
each of the four downlink types, up to 2048 body bytes each, separately from the completed-message buffer.
Only consecutive indices starting at zero, with matching IDs and chunk counts, can complete a message.
Malformed frames, gaps, duplicate continuations, changed IDs/counts, and overflow invalidate the affected
assembly; a fresh chunk zero starts it again. Other message types can arrive between its chunks.

This replaces the previous 5-byte envelope and is not wire-compatible with it. Deploy Kelvin.Server,
rebuild Kelvin.Simulator, and reflash all HMI panels together. Gateway and Node firmware need no update.
IDs identify assemblies, not acknowledgements or guaranteed delivery; interrupted messages still require
a new full snapshot. IDs can repeat after sender restart or counter wraparound.

The native regression tests compile the real reassembler and uplink encoder without Arduino hardware.
From the repository root, with a C++ compiler available:

```powershell
g++ -std=c++17 -Wall -Wextra -Werror -I src/Kelvin.Devices/Common/src src/Kelvin.Devices/Hmi/tests/HmiFrameReassemblerTests.cpp src/Kelvin.Devices/Hmi/src/protocol/HmiFrameReassembler.cpp src/Kelvin.Devices/Hmi/src/protocol/HmiCommandEncoder.cpp -o "$env:TEMP/kelvin-hmi-tests.exe"
& "$env:TEMP/kelvin-hmi-tests.exe"
```

MSVC is also supported: run `cl` from a Developer PowerShell with `/std:c++17 /EHsc /W4 /WX`, the same
include directory (`/I`) and sources, and an output path specified with `/Fe`.

## Battery and power

The panel is powered through USB from an external charger and regulated 5V boost converter. Its onboard
battery connector is left disconnected. An [Adafruit LC709203F (product 4712)](https://www.adafruit.com/product/4712)
monitors the actual single-cell 3.7V LiPo pack, not the boosted USB supply. Connect the battery directly
to the 6106 charger's BATT connector. Connect the charger's BAT pad to the positive contact of either
fuel-gauge battery JST-PH socket and charger GND to its negative contact; leave the other gauge socket
unused. This is a parallel sensing branch: panel load current stays in the charger/boost wiring, not
the gauge's connectors or PCB traces. Use BAT, not the boosted 5V output or power-path load output,
for this sensing connection. Verify connector polarity against the PCB markings before connecting.

Connect gauge VIN to the HMI's regulated **3.3V** rail, GND to common ground, SDA to GPIO8 and SCL to GPIO9.
It shares the existing 400kHz `Wire` bus with touch, the IO helper and the SHT40. Do not power gauge VIN
from USB 5V: its I2C pull-ups must be at the ESP32's 3.3V logic level. INT and THERM are not connected.
See the [HMI wiring guide](../../../docs/Kelvin%20HMI%20Wiring%20Diagram.md) for the full power path.

The [Adafruit bq25185 charger with 5V boost (6106)](https://www.adafruit.com/product/6106) supplies the
panel. Use its 5V/GND output to power
the panel's USB input through a suitable USB power adapter/cable; the charger's USB-C socket is an input.
Do not connect competing USB power sources to the panel. Check converter startup under the actual panel
load; Adafruit documents a startup-load limitation on the TPS61023 boost. The bq25185 also has a fixed
six-hour charging safety timeout: a depleted 10000mAh pack cannot fully charge at 1A within one such cycle.

`BatteryMonitor` reads the gauge's percentage directly, rounded to a whole percent, with CRC-checked
reads. Missing hardware, invalid readings or I2C errors produce `-1` (unknown), not an empty-battery
reading; later polls retry initialization. The gauge stays operating while the ESP32 sleeps.

Configuration in `Config.h`:

- `BATTERY_PACK_APA` defaults to `0x36`, Adafruit's 3000mAh preset, the largest preset in its library.
  This is a starting point, **not a calibrated 10000mAh profile**. Validate the reported percentage over
  a full charge/discharge cycle and obtain the appropriate APA for the actual pack; do not enter `10000`
  into this one-byte register or scale the percentage by pack capacity.
- `BATTERY_TEMPERATURE_C` defaults to 25 C. With no battery thermistor attached, firmware uses I2C
  temperature mode and supplies this fixed assumed pack temperature. Adjust it for the installation;
  this is not a battery-temperature measurement or charging-safety control.

The charging bolt is an estimate from successive valid fuel-gauge percentages at 0.1% resolution:
an increase sets `externalPower` true and a decrease clears it. Equal readings retain the last state,
including at 100%; the first reading after boot only establishes a baseline and starts without a bolt.
Failed reads do not change the tracked state or baseline. Light sleep preserves both. Small fluctuations
can change the indicator, and detection waits until the next battery poll observes a change.
This is not a direct charger-status or external-power signal: the boosted USB supply is present on both
battery and charger power, and the gauge itself cannot distinguish them.

## Screen timeout

The backlight turns off after 60 seconds without a touch, configured by `DISPLAY_IDLE_TIMEOUT_MS` in
`Config.h`. Touching the screen turns it back on; that first touch is ignored by the controls until
released, so waking the panel cannot accidentally change a setting. The timeout applies on both battery
and external power.

After the backlight turns off, the ESP32 enters light sleep with Wi-Fi/ESP-NOW stopped and the RGB panel
driver released. The GT911 touch controller stays powered; its interrupt on GPIO4 wakes the ESP32.
The interrupt polarity is read from the controller's configuration rather than assumed. RAM and the
current UI page are retained. The timeout applies on external power too.

A timer wakes the ESP32 for the normal sensor-reading interval without lighting the screen. Touch wake
restores the radio before restarting the RGB driver, redraws the screen, and resynchronizes RGB DMA
at VSYNC. The backlight stays off until complete frame boundaries have passed. This prevents radio
startup from interrupting a newly running RGB stream and avoids exposing a partially refreshed frame.
Unchanged preference replies do not rewrite NVS; actual preference changes redraw and resynchronize
RGB after the flash writes, which can otherwise disrupt PSRAM bounce-buffer delivery.
The panel takes a fresh sensor reading and requests thermostat settings,
control state and preferences. Requests repeat every five seconds until all three replies parse
successfully. Radio pushes sent while asleep are missed, so the wake snapshot restores changes made
while the panel was asleep. The server must be updated and restarted along with the firmware.

If sleep setup fails, the screen is restored and the error is logged; another idle timeout retries sleep.

To check on hardware, leave the panel untouched for a minute, then touch a control while the screen is
dark: the screen should wake without changing the setting. Release and touch again to use the control.
Touching while the screen is on should restart the one-minute timeout, including on the settings page.
Also check that timer sensor updates continue with the screen dark, and change thermostat settings and
preferences from another client while asleep to verify that they refresh after touch wake. Measure idle
current on the real board to confirm power savings; light sleep does not power down every board component.

## Directory layout

- `Hmi.ino` - setup/loop: reads the sensor, drains downlink messages, ticks LVGL.
- `Config.h` - gateway MAC, sensor bus pins, display resolution (confirmed 1024x600).
- `src/display/RgbPanel.h`/`.cpp` - installs the ESP-IDF RGB panel driver (pins/timing from the official demo).
- `src/io/Gt911Touch.h`/`.cpp` - reads the GT911 touch controller over `Wire`.
- `src/io/IoExtension.h`/`.cpp` - drives the IO extension chip's Mode/Output registers directly (backlight,
  touch-reset).
- `src/battery/` - CRC-checked battery voltage/percentage reads via the external Adafruit LC709203F.
- `src/protocol/` - `HmiCommandEncoder` (builds outgoing command bytes), `HmiFrameReassembler` (reassembles
  any chunked downlink message via the shared HmiEnvelope header, including the thermostat state pushes),
  `ThermostatStateParser` (decodes a reassembled state
  blob into a `ThermostatState` struct), `ControlStateParser` (decodes the small, unchunked
  `ControlStateChanged` push into a `ControlCallState` struct) and `EnvironmentReadingParser` (decodes the
  small, unchunked `EnvironmentReadingChanged` push - the real-time system-wide average reading - into an
  `EnvironmentAverageReading` struct).
- `src/ui/` - `Ui` (main screen: current temperature, stats, set point slider, mode/fan controls and the gear
  button), `SettingsPage` (full-screen page: heating/cooling schedule timelines + edit sheet, and forecast
  lockouts), `UiTheme` (shared colors/fonts), `UiWidgets` (shared flat button/slider styling),
  `TemperatureFormat` (Celsius wire values <-> the preferred-unit display synced via PreferencesChanged), and `fonts/` (generated 96px JetBrains Mono
  digits font under the OFL, see `UiTheme::fontHuge`).
- `../Common/src/HmiProtocol.h` - the wire message types and byte layouts, mirrored from Kelvin.Server's
  `Models/HmiMessageType.cs`/`Models/FrameTags.cs`/`Features/Hmi/HmiThermostatStateEncoder.cs`/
  `Features/Hmi/HmiControlStateEncoder.cs`. Keep them in sync if either side changes.

## Shared code

`Communicator` (ESP-NOW send/receive) and `EnvironmentMonitor` (SHT4x reading + change/heartbeat filtering)
now live in `../Common` alongside Node, since both devices need the same ESP-NOW framing and the same onboard
sensor. `Common` is a proper Arduino library (`library.properties` + `src/`) rather than a plain include
folder, specifically so its `.cpp` files get compiled once and linked into whichever sketch depends on it -
that's why compiling either Node or Hmi now requires the extra `--libraries "src/Kelvin.Devices"` flag.
