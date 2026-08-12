# Kelvin Simulator

CLI simulator for manually testing Kelvin with gateway-like sensor packets.

## What it does

- Opens a serial port as a gateway emulator.
- Responds to the `info` handshake Kelvin.Server uses to discover gateways.
- Emits sensor packets using the same packet shape as the current device firmware.
- Starts with 5 simulated sensors by default.
- Uses a shared ambient temperature that rises/falls gradually, plus per-sensor room drift.
- Polls Kelvin.Server's `/api/thermostat` and `/api/control/state` endpoints so the `auto` scenario
  can track the server's real heating/cooling/idle state.

## CLI options

- `--server-url` Kelvin.Server base URL. Defaults to `http://localhost:5000`.
- `--port` virtual COM port. Required.
- `--sensor-count` starting sensor count. Defaults to 5.
- `--base-temp` starting environment temperature in Celsius. Defaults to 21.5.
- `--interval` packet emit cadence (e.g. `00:00:30`). Defaults to 30 seconds.
- `--non-interactive` disables the live command loop for scripted runs.
- `--debug` (or `--debug=info`) logs on every call/mode/target change plus any HTTP
  errors/exceptions talking to Kelvin.Server. `--debug=verbose` additionally logs the raw
  request/response (URI, status code, body) for every poll and the resolved ambient directive
  every simulation tick.

Each option also accepts an `=` form, e.g. `--port=COM5`.

## Live commands

When running interactively, the simulator accepts these commands:

- `base <temp>` changes the shared environment temperature.
- `add` adds one sensor.
- `remove <index>` removes a sensor by index.
- `enable <index>` marks one sensor online.
- `disable <index>` marks one sensor offline.
- `enable all` marks every sensor online.
- `disable all` marks every sensor offline.
- `scenario <auto|idle|heating|cooling>` switches the ambient trend mode. `auto` follows the
  server's control call state first, then falls back to thermostat mode; the others force a fixed
  ambient target direction.
- `list` prints the current sensor roster.
- `status` prints the current simulator state.

Any unrecognized input prints `status`.

## Current scope

- CLI-only.
- Windows-first.
- Designed to run against a virtual COM pair created by an OS-level serial driver.
  - [Free Virtual Serial Ports](https://freevirtualserialports.com/) was used for development.
  - Only requires a simple bridge between COM ports.

## Virtual serial port dependency

The end-to-end smoke check (Kelvin.Server discovering the simulated gateway) depends on a working
virtual serial port setup. That means:

- the OS driver must already be installed,
- the virtual COM pair must exist,
- one end must be assigned to the simulator via `--port`,
- and the other end must be visible to Kelvin.Server so it can discover the gateway.
