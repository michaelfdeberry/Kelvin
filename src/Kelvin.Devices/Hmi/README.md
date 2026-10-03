# Kelvin Hmi Panel

Firmware for the Kelvin wall-mounted thermostat panel: a 7" touch display that mirrors the Node's onboard
environment reading and adds full thermostat control (mode, set points, schedules, forecast lockouts) sent
to a Kelvin gateway over ESP-NOW, using the same wire protocol documented in `Common/src/HmiProtocol.h`.

## Board

Waveshare **ESP32-S3-Touch-LCD-7B** (the "B" revision - not the plain "-7"; the two look similar but have
different resolutions/timings/IO-expander chips, see below). The esp32 Arduino core has no dedicated board
definition for the "7B" specifically, so this sketch uses the generic `waveshare_esp32_s3_touch_lcd_7` FQBN
(same ESP32-S3 module/pinout/flash/PSRAM) combined with a fully custom `esp_panel_board_custom_conf.h` - see
below.

1024x600 IPS RGB-parallel LCD (Waveshare's docs call the chip "ST7701", but the official demo code drives
it with zero init commands exactly like the simpler "dumb RGB" ST7262 controller - see
`esp_panel_board_custom_conf.h`'s `LCD_CONTROLLER` comment) + a register-mapped "IO EXTENSION" helper chip
(I2C address `0x24`; same chip `src/battery/BatteryMonitor.cpp` reads for the ADC, Mode/Output registers
now also driven by `src/io/IoExtension.h`) for backlight/touch-reset + GT911 capacitive touch over I2C. All
pin/timing/register values are confirmed via Waveshare's official Arduino demo source
(`github.com/waveshareteam/ESP32-S3-Touch-LCD-7B`, `examples/Arduino/examples/06_LCD` and `08_TOUCH`), not
guessed - see `src/ui/Ui.cpp`'s `begin()` for the `esp_panel::board::Board` integration (RGB panel/GT911
touch) plus the manual `IoExtension`-driven backlight/touch-reset, since this chip doesn't match any
IO-expander type `ESP32_Display_Panel` supports natively.

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

Compile from the repository root.

```powershell
arduino-cli compile --fqbn "esp32:esp32:waveshare_esp32_s3_touch_lcd_7:PSRAM=enabled,PartitionScheme=huge_app" --libraries "src/Kelvin.Devices" "src/Kelvin.Devices/Hmi"
```

Upload from the repository root.

```powershell
arduino-cli compile --fqbn "esp32:esp32:waveshare_esp32_s3_touch_lcd_7:PSRAM=enabled,PartitionScheme=huge_app" --libraries "src/Kelvin.Devices" --upload -p COMX "src/Kelvin.Devices/Hmi"
```

Replace `COMX` with the board's actual serial port.

## Directory layout

- `Hmi.ino` - setup/loop: reads the sensor, drains downlink messages, ticks LVGL.
- `Config.h` - gateway MAC, sensor bus pins, display resolution (confirmed 1024x600).
- `esp_panel_board_custom_conf.h`/`esp_panel_board_supported_conf.h`/`esp_panel_drivers_conf.h`/
  `esp_utils_conf.h` - per-sketch config overrides for the `ESP32_Display_Panel` library (same "copy into
  your sketch and edit" convention as `lv_conf.h`). This sketch uses the CUSTOM board path (not the
  library's built-in Waveshare profile, which is for the plain "-7" board, not this "-7B") - besides the
  different resolution/timing, this also lets `ESP_PANEL_BOARD_TOUCH_BUS_SKIP_INIT_HOST` be set to `1` (the
  GT911 touch shares the exact I2C bus/pins `Hmi.ino`'s own `Wire.begin()` already uses for
  `EnvironmentMonitor`/`BatteryMonitor`, and installing the I2C driver on that port twice fails), and
  `ESP_PANEL_BOARD_USE_BACKLIGHT`/`USE_EXPANDER` to be disabled entirely (this board's IO-extension chip
  isn't one the library's expander abstraction supports - see `src/io/IoExtension.h`).
- `src/io/IoExtension.h`/`.cpp` - drives the IO extension chip's Mode/Output registers directly (backlight,
  touch-reset), bypassing `ESP32_Display_Panel`'s expander abstraction for the reason above.
- `src/battery/` - battery voltage/percentage via the same IO extension chip's I2C ADC register.
- `src/protocol/` - `HmiCommandEncoder` (builds outgoing command bytes), `HmiStateDecoder` (reassembles
  chunked thermostat state pushes from the server), `ThermostatStateParser` (decodes a reassembled state
  blob into a `ThermostatState` struct), `ControlStateParser` (decodes the small, unchunked
  `ControlStateChanged` push into a `ControlCallState` struct) and `EnvironmentReadingParser` (decodes the
  small, unchunked `EnvironmentReadingChanged` push - the real-time system-wide average reading - into an
  `EnvironmentAverageReading` struct).
- `src/ui/` - `Ui` (main screen: dial, mode/fan controls, onboard sensor card - now driving the real RGB
  panel/GT911 touch via `esp_panel::board::Board`), `ThermostatEditor` (the Set Points/Heat Schedules/Cool
  Schedules edit modal), `UiTheme` (shared colors/fonts), `TemperatureFormat` (Celsius wire values <->
  Fahrenheit display).
- `../Common/src/HmiProtocol.h` - the wire message types and byte layouts, mirrored from Kelvin.Server's
  `Models/HmiMessageType.cs`/`Models/FrameTags.cs`/`Features/Hmi/HmiThermostatStateEncoder.cs`/
  `Features/Hmi/HmiControlStateEncoder.cs`. Keep them in sync if either side changes.

## Shared code

`Communicator` (ESP-NOW send/receive) and `EnvironmentMonitor` (SHT4x reading + change/heartbeat filtering)
now live in `../Common` alongside Node, since both devices need the same ESP-NOW framing and the same onboard
sensor. `Common` is a proper Arduino library (`library.properties` + `src/`) rather than a plain include
folder, specifically so its `.cpp` files get compiled once and linked into whichever sketch depends on it -
that's why compiling either Node or Hmi now requires the extra `--libraries "src/Kelvin.Devices"` flag.
