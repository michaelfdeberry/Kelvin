# Kelvin Simulator

CLI simulator for manually testing Kelvin with gateway-like sensor packets.

## What it does

- Opens a serial port as a gateway emulator.
- Responds to the `info` handshake Kelvin.Server uses to discover gateways.
- Emits sensor packets using the same packet shape as the current device firmware.
- Starts with 5 simulated sensors by default.
- Uses a base environment temperature with per-sensor variation.
- Polls Kelvin.Server for thermostat and control-state snapshots when a server URL is configured.

## Live commands

When running interactively, the simulator accepts these commands:

- `base <temp>` changes the shared environment temperature.
- `add` adds one sensor.
- `remove <index>` removes a sensor by index.
- `enable <index>` marks one sensor online.
- `disable <index>` marks one sensor offline.
- `enable all` marks every sensor online.
- `disable all` marks every sensor offline.
- `scenario <auto|idle|heating|cooling>` switches the temperature drift mode.
- `list` prints the current sensor roster.
- `status` prints the current simulator state.

## Current scope

- CLI-only.
- Windows-first.
- Designed to run against a virtual COM pair created by an OS-level serial driver.

## Phase 2 dependency

The full end-to-end smoke check depends on a working virtual serial port setup.
That means:

- the OS driver must already be installed,
- the virtual COM pair must exist,
- one end must be assigned to the simulator,
- and the other end must be visible to Kelvin.Server so it can discover the gateway.

Until that is in place, the simulator can still be developed and unit-tested, but the live gateway discovery smoke test is Phase 2.

## Suggested research targets

- com0com + hub4com for a free Windows setup.
- A commercial GUI serial-port driver if ease of setup matters more than licensing.
- Any driver that gives you a stable named COM pair that Kelvin.Server can enumerate.

## Planned CLI settings

- `--server-url` Kelvin.Server base URL.
- `--port` virtual COM port.
- `--sensor-count` starting sensor count.
- `--base-temp` starting environment temperature.
- `--interval` packet emit cadence.
- `--non-interactive` disables the command loop for scripted runs.

## Next implementation steps

1. Add live sensor add/remove commands.
2. Poll Kelvin thermostat/control endpoints for heating and cooling scenarios.
3. Refine packet emission into a reusable simulator engine.
