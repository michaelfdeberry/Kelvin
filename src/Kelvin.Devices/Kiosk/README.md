# Kelvin Kiosk

Kelvin Kiosk is a Raspberry Pi Python service that reads a supported environmental sensor, submits readings to the Kelvin SignalR readings hub, and opens the existing Kelvin web UI in fullscreen Chromium.

## Features

- Supports the `sht4x` and `scd4x` sensor families shared with the Node device
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
- `systemd/kelvin-autologin.conf` tty1 kiosk-user auto-login override

## Setup

1. Create and activate a Python 3.11+ virtual environment.
2. Install dependencies with `pip install -r requirements.txt`.
3. Copy `.env.example` to `.env` and update the settings for your Pi.
4. Run `python -m kelvin_kiosk.main --once` to verify sensor reads and hub submission.
5. Run `./scripts/install-pi.sh` on Raspberry Pi OS Lite to install dependencies, Chromium, Xorg, Openbox, the sensor service, and the tty1 login configuration.
6. Reboot the Pi to start the kiosk display session.

The installer configures `getty@tty1` to automatically log in the installing user. That login session runs `startx`
and Openbox, which launches Chromium from the same user account. This gives Xorg the console session required to
own `/dev/tty1` without enabling root Xorg access. The background `kelvin-kiosk.service` only sends sensor readings;
it does not launch Chromium. Leave `/dev/tty1` available for the kiosk.

### Display Verification

After reboot, the X display runs on `:0`. From an SSH shell logged in as the kiosk user, launch the one-shot
process with its X session variables:

```bash
DISPLAY=:0 XAUTHORITY="$HOME/.Xauthority" python -m kelvin_kiosk.main --once
```

`--once` launches Chromium before it reads and submits a sensor sample. It requires a reachable API to finish;
when the API is unavailable it continues retrying after the browser opens. To test only the display while the API
is offline, run:

```bash
DISPLAY=:0 XAUTHORITY="$HOME/.Xauthority" chromium --kiosk "http://192.168.1.50:5209"
```

Set `KELVIN_SERVER_URL` and `KELVIN_UI_URL` to a resolvable address before starting the service. `kelvin.local`
only works when the Kelvin server advertises that mDNS name; use its LAN IP address (for example,
`http://192.168.1.50:5209`) when mDNS is unavailable.

## Configuration

The service reads configuration from environment variables or a local `.env` file.

- `KELVIN_SERVER_URL` base URL for the Kelvin server, such as `http://kelvin.local:5209`
- `KELVIN_UI_URL` fullscreen URL to launch in Chromium
- `KELVIN_SENSOR_TYPE` one of `sht4x`, `scd4x`, or `mock`
- `KELVIN_POLL_INTERVAL_SECONDS` read cadence, default `30`
- `KELVIN_HEARTBEAT_SECONDS` forced send interval, default `300`
- `KELVIN_FAILURE_BACKOFF_SECONDS` wait time after a failed read or hub send, default `10`
- `KELVIN_BROWSER_RESTART_SECONDS` minimum delay before relaunching Chromium after exit, default `5`
- `KELVIN_THRESHOLD_TEMPERATURE_C` default `0.5`
- `KELVIN_THRESHOLD_HUMIDITY_PERCENT` default `1.0`
- `KELVIN_THRESHOLD_CO2_PPM` default `75`
- `KELVIN_LOG_LEVEL` Python log level, default `INFO`
- `KELVIN_BROWSER_ENABLED` set to `true` to launch Chromium automatically
- `KELVIN_BROWSER_COMMAND` override the Chromium executable if needed
- `KELVIN_CHROMIUM_ARGS` optional extra Chromium arguments
- `KELVIN_MAC_INTERFACE` optional network interface to use for identity and the kiosk UI URL
- `KELVIN_I2C_PORT` I2C device file used by the `sht4x`/`scd4x` sensors, default `/dev/i2c-1`

## Notes

- The server must expose the readings hub at `/hubs/readings`.
- Battery is intentionally sent as `null` for kiosk readings.
- The included `mock` sensor mode is useful for development without hardware.
- The kiosk loop retries after sensor and SignalR failures instead of terminating the process.
- Chromium is relaunched automatically if it exits unexpectedly.
- Chromium is launched with `?mac=<address>` appended to the UI URL. The Kelvin client uses that to enter
  kiosk mode: it shows only the sensor with a matching MAC address, hides the sidebar and analytics, and
  applies a layout tuned for the 1280x800 panel.
