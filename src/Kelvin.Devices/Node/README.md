# Kelvin Sensor Node

Firmware for the Kelvin environmental sensor node. The node reads an SHT40 sensor, shows readings on a 240x320 SPI display, and sends changed readings to a Kelvin gateway over ESP-NOW.

## Board

The node is built for the ESP32-S3 PowerFeather V2 board. The deployed hardware is a V2R2 revision; select the `V2` board revision in Arduino tooling because the ESP32 Arduino core exposes V1 and V2 targets.

Arduino CLI fully qualified board name:

```text
esp32:esp32:esp32s3_powerfeather:Revision=V2
```

The board has a JST-PH battery connector, USB-C power, a STEMMA QT connector, an onboard user button, and a reset button. Check the battery polarity printed on the board before connecting a battery.

## Setup

Install the required Arduino libraries from the Node directory:

```powershell
.\Node.bat
```

The script installs the Sensirion, LovyanGFX, and PowerFeather libraries. The active firmware sensor is SHT40; the DHT and SCD4x libraries remain available for alternate sensor configurations.

Compile from the repository root:

```powershell
arduino-cli compile --fqbn "esp32:esp32:esp32s3_powerfeather:Revision=V2" "src/Kelvin.Devices/Node"
```

To flash the board, connect it over USB-C and select the matching ESP32-S3 PowerFeather V2 board and serial port in Arduino IDE or Arduino CLI. If the board does not accept an upload, hold `BTN`, press `RST` momentarily, then release `BTN` to enter download mode.

## Pin Map

### PowerFeather board

| Function              | GPIO | Configuration                                      |
| --------------------- | ---: | -------------------------------------------------- |
| Onboard `BTN`         |    0 | `INPUT_PULLUP`; independent shutdown button        |
| Onboard user LED      |   46 | Managed by the board/SDK; not used by the node     |
| Battery charger alarm |   21 | Managed by the board/SDK; not used by the node     |
| Charger interrupt     |    5 | Managed by the board/SDK; not used by the node     |
| Context button        |   15 | External/context button and deep-sleep wake source |

The onboard PowerFeather button and the context button are separate inputs. The context button wakes the display and controls its existing interaction. Holding the onboard `BTN` for 3 seconds requests shutdown mode.

### STEMMA QT sensor bus

| Function      |   GPIO |
| ------------- | -----: |
| I2C SDA       |     47 |
| I2C SCL       |     48 |
| SHT40 address | `0x44` |

The node enables `VSQT` before initializing and reading the SHT40 sensor. `VSQT` is disabled before deep sleep.

### SPI display

The display is a 240x320 ST7789 panel configured in `src/display/Display.cpp`:

| Function      |      GPIO |
| ------------- | --------: |
| Backlight PWM |  11 (D13) |
| Display CS    |  12 (D12) |
| Display DC    |  17 (D09) |
| Display reset |  18 (D10) |
| SPI MOSI      | 40 (MOSI) |
| SPI SCLK      |  39 (SCK) |

The display uses SPI3, 40 MHz write frequency, rotation 1, and LovyanGFX. It is powered from the `3V3` header pin, enabled only while the display is in use.

## Configuration

The main hardware and application settings are in `Config.h`:

- Battery capacity: `1000 mAh`
- Maximum charging current: `100 mA`
- Onboard shutdown hold duration: `3000 ms`
- Gateway MAC address: configured by `GATEWAY_MAC_ADDRESS_BYTES`
- Debug serial logging: enabled by `DEBUG`

Use a charging current appropriate for the connected battery. The configured capacity is passed to `Board.init()` and is used by the PowerFeather fuel gauge.

## Startup and sleep behavior

Each wake cycle runs from `setup()` and ends in deep sleep; `loop()` is not used.

1. Read the wake cause.
2. Initialize I2C, the PowerFeather board, and the ESP-NOW communicator.
3. Enable `VSQT`, initialize the battery monitor, and read the SHT40.
4. If the wake came from the context button, enable `3V3` and keep the display interaction available for 30 seconds, then disable `3V3`.
5. Send the payload when readings changed enough or the heartbeat interval elapsed.
6. Stop ESP-NOW, disable `VSQT` and `3V3`, arm the timer and context-button wake sources, and enter deep sleep.

The context button uses ESP32-S3 `ext0` wake on a low level. Its RTC-domain pull-up is configured before deep sleep so the input does not float while the digital GPIO domain is powered down. Timer wake occurs every 30 seconds.

## Shutdown mode and battery replacement

Hold the onboard PowerFeather `BTN` for 3 seconds while the node is running from the battery. The firmware checks that the charger does not report a good external supply, then calls `Board.enterShutdownMode()`.

Shutdown mode consumes approximately 1.4 uA and leaves only the battery charger and fuel gauge powered. It is suitable for disconnecting or replacing the battery after the board has entered shutdown.

Important recovery behavior:

- Shutdown is rejected when USB-C or another good external supply is connected.
- The onboard `BTN` cannot wake the board from shutdown.
- Connect a charger-recognized good USB-C or DC supply to exit shutdown and restart the board.
- Confirm battery polarity before installing the replacement battery.

## Source layout

- `Node.ino` - startup, wake sources, button handling, sleep, and main cycle.
- `Config.h` - board pins and node settings.
- `src/environment/` - SHT40 readings and heartbeat/change filtering.
- `src/battery/` - PowerFeather battery setup, charge reporting, and shutdown entry.
- `src/display/` - LovyanGFX display driver and context-button interaction.
- `src/communication/` - ESP-NOW gateway communication.
- `../Common/SensorPayload.h` - shared node/gateway payload contract.
