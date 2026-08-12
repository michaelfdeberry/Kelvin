# Kelvin Kiosk

Kelvin Kiosk is a Raspberry Pi Python service that reads a supported environmental sensor, submits readings to the Kelvin SignalR readings hub, and opens the existing Kelvin web UI in fullscreen Chromium.

## Features

- Supports the same sensor families as the Node device: `dht11`, `sht4x`, and `scd4x`
- Publishes readings to `EnvironmentReadingsHub.SubmitReading`
- Uses the Raspberry Pi network MAC address as the device identity
- Omits battery data for mains-powered kiosk hardware
- Launches the Kelvin client URL in Chromium kiosk mode
- Includes `systemd` and startup script assets for Pi provisioning

## Layout

- `kelvin_kiosk/` Python package
- `requirements.txt` Python dependencies
- `.env.example` environment configuration template
- `scripts/start-kiosk.sh` launcher used by `systemd`
- `systemd/kelvin-kiosk.service` sample service unit

## Setup

1. Create and activate a Python 3.11+ virtual environment.
2. Install dependencies with `pip install -r requirements.txt`.
3. Copy `.env.example` to `.env` and update the settings for your Pi.
4. Run `python -m kelvin_kiosk.main --once` to verify sensor reads and hub submission.
5. Install the `systemd` unit and Chromium kiosk autostart after local verification.

## Configuration

The service reads configuration from environment variables or a local `.env` file.

- `KELVIN_SERVER_URL` base URL for the Kelvin server, such as `http://kelvin.local:5194`
- `KELVIN_UI_URL` fullscreen URL to launch in Chromium
- `KELVIN_SENSOR_TYPE` one of `dht11`, `sht4x`, `scd4x`, or `mock`
- `KELVIN_POLL_INTERVAL_SECONDS` read cadence, default `30`
- `KELVIN_HEARTBEAT_SECONDS` forced send interval, default `300`
- `KELVIN_THRESHOLD_TEMPERATURE_C` default `0.5`
- `KELVIN_THRESHOLD_HUMIDITY_PERCENT` default `1.0`
- `KELVIN_THRESHOLD_CO2_PPM` default `75`
- `KELVIN_BROWSER_ENABLED` set to `true` to launch Chromium automatically
- `KELVIN_BROWSER_COMMAND` override the Chromium executable if needed
- `KELVIN_CHROMIUM_ARGS` optional extra Chromium arguments
- `KELVIN_MAC_INTERFACE` optional network interface to use for identity

## Notes

- The server must expose the readings hub at `/hubs/readings`.
- Battery is intentionally sent as `null` for kiosk readings.
- The included `mock` sensor mode is useful for development without hardware.