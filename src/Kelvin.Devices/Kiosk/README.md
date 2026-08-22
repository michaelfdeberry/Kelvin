# Kelvin Kiosk

Kelvin Kiosk is a Raspberry Pi Python service that reads an `scd4x` CO2/temperature/humidity sensor, submits readings to the Kelvin server's `/api/sensors/packets` REST endpoint, and opens the existing Kelvin web UI in fullscreen Chromium.

## Features

- Reads the `scd4x` sensor (CO2, temperature, humidity)
- Publishes readings via HTTP POST to `/api/sensors/packets`
- Uses the Raspberry Pi network MAC address as the device identity
- Omits battery data for mains-powered kiosk hardware
- Runs Chromium fullscreen under `cage`, a minimal single-application Wayland compositor
- Includes `systemd` unit files for the sensor service and the display service

## Layout

- `kelvin_kiosk/` Python package
- `requirements.txt` Python dependencies
- `.env.example` environment configuration template
- `scripts/start-kiosk.sh` launcher used by `kelvin-kiosk.service` (sensor readings)
- `scripts/start-browser.sh` launcher used by `kelvin-kiosk-display.service` (Chromium under `cage`)
- `systemd/kelvin-kiosk.service` sensor service unit
- `systemd/kelvin-kiosk-display.service` display service unit; runs `cage` + Chromium directly on `tty1`

## Setup

1. On the Raspberry Pi (running Raspberry Pi OS Lite), clone the repo:
   `git clone https://github.com/michaelfdeberry/Kelvin.git`
2. `cd Kelvin/src/Kelvin.Devices/Kiosk` and run `./scripts/install-pi.sh`. It creates the `.venv`, installs
   Python dependencies, installs `cage`, Chromium, and the Noto Color Emoji font, copies `.env.example` to
   `.env` (if one doesn't already exist), and installs both `systemd` services.
3. Edit `.env` with the settings for your Pi (server URL, thresholds, etc.).
4. Reboot the Pi to start the kiosk display.

The installer enables two independent services: `kelvin-kiosk.service` reads the sensor and submits readings, and
`kelvin-kiosk-display.service` runs `cage`, which owns `tty1` directly (via `PAMName=login` and `TTYPath` in the
unit) and launches Chromium fullscreen as its only client. Because `cage` claims `tty1` itself, the installer
disables the console `getty@tty1.service` so the two don't compete for the same tty.

The installer also enables the I2C interface (`raspi-config nonint do_i2c 0`), which Raspberry Pi OS disables by
default and which the `scd4x` sensor needs (`/dev/i2c-1`). This requires the reboot in step 4 to take effect; if
`kelvin-kiosk.service` logs `FileNotFoundError: ... /dev/i2c-1` after installing, either the Pi hasn't been
rebooted yet or I2C is still disabled - run `sudo raspi-config nonint do_i2c 0` and reboot, then confirm the
sensor is visible with `i2cdetect -y 1` (it should show address `62`).

### Verifying the services

Sensor readings and hub submission:

```bash
sudo journalctl -u kelvin-kiosk.service -f
```

The display service:

```bash
sudo journalctl -u kelvin-kiosk-display.service -f
```

Set `KELVIN_SERVER_URL` and `KELVIN_UI_URL` to a resolvable address before starting the service. `kelvin.local`
only works when the Kelvin server advertises that mDNS name; use its LAN IP address (for example,
`http://192.168.1.50:5209`) when mDNS is unavailable.

## Configuration

The service reads configuration from environment variables or a local `.env` file.

- `KELVIN_SERVER_URL` base URL for the Kelvin server, such as `http://kelvin.local:5209`
- `KELVIN_UI_URL` fullscreen URL to launch in Chromium
- `KELVIN_POLL_INTERVAL_SECONDS` read cadence, default `30`
- `KELVIN_HEARTBEAT_SECONDS` forced send interval, default `300`
- `KELVIN_FAILURE_BACKOFF_SECONDS` wait time after a failed read or hub send, default `10`
- `KELVIN_THRESHOLD_TEMPERATURE_C` default `0.5`
- `KELVIN_THRESHOLD_HUMIDITY_PERCENT` default `1.0`
- `KELVIN_THRESHOLD_CO2_PPM` default `75`
- `KELVIN_LOG_LEVEL` Python log level, default `INFO`
- `KELVIN_BROWSER_ENABLED` set to `false` to have `kelvin-kiosk-display.service` run `cage` without launching Chromium
- `KELVIN_BROWSER_COMMAND` override the Chromium executable if needed
- `KELVIN_CHROMIUM_ARGS` optional extra Chromium arguments
- `KELVIN_MAC_INTERFACE` optional network interface to use for identity and the kiosk UI URL
- `KELVIN_I2C_PORT` I2C device file used by the `scd4x` sensor, default `/dev/i2c-1`

## Troubleshooting

Remote debugging can be enabled for the kiosk browser by adding `--remote-debugging-port=9222` to the `KELVIN_CHROMIUM_ARGS`.

To get information about the site you can use curl from the ssh commandline.

```
curl -s http://localhost:9222/json
```
