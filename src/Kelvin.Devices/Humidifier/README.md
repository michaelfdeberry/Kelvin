# Kelvin Humidifier

Firmware for the Kelvin humidifier controller. An ESP32, powered over USB, receives the humidifier state the server pushes through the Gateway and drives the solenoid relay from it. Wiring and power are covered in [Kelvin Humidifier Wiring Diagram.md](../../../docs/Kelvin%20Humidifier%20Wiring%20Diagram.md).

## Board

Generic ESP32 dev module:

```text
esp32:esp32:esp32
```

## Setup

Set `GATEWAY_MAC_ADDRESS_BYTES` and check `RELAY_PIN`/`RELAY_ACTIVE_HIGH` in `Config.h`, then install the libraries:

```powershell
.\Humidifier.bat
```

Compile from the repository root:

```powershell
arduino-cli compile --fqbn "esp32:esp32:esp32" --libraries "src/Kelvin.Devices" "src/Kelvin.Devices/Humidifier"
```

Upload from the repository root:

```powershell
arduino-cli compile --fqbn "esp32:esp32:esp32" --libraries "src/Kelvin.Devices" --upload -p COMX "src/Kelvin.Devices/Humidifier"
```

## Behavior

- Checks in at boot and every `CHECK_IN_INTERVAL_MS` (5 minutes) with the current relay state. A check-in the Gateway doesn't acknowledge is retried after `COMMUNICATOR_RETRY_MS`.
- Sets the relay to every `State` the server sends and answers each with a `StateAck`.
- Releases the relay if no `State` has arrived for `STATE_TIMEOUT_MS` (2 minutes). The server resends every 30 seconds while the humidifier should run.
- Retries starting ESP-NOW every `COMMUNICATOR_RETRY_MS` (5 seconds) if it fails at boot.
- Ignores frames that don't come from the Gateway's MAC address.
- Boots with the relay released. The solenoid's 24V AC comes from the humidifier transformer, which is only energized while the furnace heats, so the valve can't open outside a heating cycle regardless of the relay.

## Protocol

Every ESP-NOW frame is tagged `KHUM` (`4B 48 55 4D`, see `Common/src/HumidifierProtocol.h` and the server's `Models/FrameTags.cs`). After the tag, each frame is the shared 9-byte device envelope (`Common/src/DeviceEnvelope.h`, `Kelvin.Protocol/DeviceEnvelope.cs`: type, chunk index, chunk count, body length, message ID) around a versioned CBOR map (same conventions as the HMI: key `0` is the schema version `1`). Every message is a single chunk.

| Type | Name       | Direction            | Body                        |
| ---: | :--------- | :------------------- | :-------------------------- |
|  `1` | `CheckIn`  | Humidifier -> Server | `{0: 1, 1: relayOn (bool)}` |
|  `2` | `State`    | Server -> Humidifier | `{0: 1, 1: enabled (bool)}` |
|  `3` | `StateAck` | Humidifier -> Server | `{0: 1, 1: relayOn (bool)}` |

The Gateway relays these frames without inspecting them. Every check-in and acknowledgement updates the device's `LastSeenAt` and the reported running state; a check-in also prompts the server to send the current state.

## Server

`Services/DeviceIntegrationService.cs` re-evaluates the humidifier (`Features/Humidifiers/SyncHumidifier.cs`) whenever the applied HVAC call changes, a device changes or checks in, and every 30 seconds. It queues a `State` through the shared device outbound channel when forced by one of those changes, while the humidifier should run (keeping the device's fail-safe satisfied), or while the device last reported running. Hysteresis uses the relay state the device last acknowledged.

## Server decision

`Features/Humidifiers/EvaluateHumidifier.cs` turns the humidifier on only when all of these hold, and fails safe to off otherwise:

1. The device is enabled. Unknown humidifiers register themselves in the `Devices` table on their first check-in, enabled, with the default configuration.
2. The current month is within the configured season (`StartMonth`..`EndMonth`, inclusive, wrapping the year).
3. Kelvin is in control (`ControlState.Enable`) and the current call is `Heating`.
4. A cached outdoor temperature is available. The humidifier never calls the weather service itself; `ThermostatService` keeps the cache warm, and a miss means off.
5. The outdoor temperature is at or below the warmest configured threshold.
6. The average indoor humidity across enabled sensors is below the target for that outdoor temperature. Once running it stays on until the target is reached; once off it waits until 2% below the target.
7. No single sensor is at or above the target while also 5% above the average.

## Configuration

Stored as JSON in `Devices.Configuration`, with the device `Type` selecting the shape (`HumidifierConfiguration`). Temperatures are stored in Celsius.

| Setting      | Default      |
| :----------- | :----------- |
| `StartMonth` | 10 (October) |
| `EndMonth`   | 3 (March)    |

Default targets. Each threshold is an upper bound: the target is the first threshold the outdoor temperature doesn't exceed, and anything warmer than the last threshold turns the humidifier off.

| Outdoor         | Target RH |
| :-------------- | --------: |
| -29°C or colder |       15% |
| -23°C           |       20% |
| -18°C           |       25% |
| -12°C           |       30% |
| -7°C            |       35% |
| -1°C            |       40% |
| +4°C            |       45% |
| above +4°C      |       off |
