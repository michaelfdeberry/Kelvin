# Kelvin Humidifier

Firmware for the Kelvin humidifier controller. An ESP32, powered from the humidifier's 24V AC transformer, asks the server through the Gateway whether the humidifier should run and drives the solenoid relay from the answer. Wiring and power are covered in [Kelvin Humidifier Wiring Diagram.md](../../../docs/Kelvin%20Humidifier%20Wiring%20Diagram.md).

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

- Pings the server every `PING_INTERVAL_MS` (15 seconds) with the current relay state.
- Sets the relay to whatever the server answers.
- Releases the relay if no answer has arrived for `RESPONSE_TIMEOUT_MS` (2 minutes).
- Retries starting ESP-NOW every `COMMUNICATOR_RETRY_MS` (5 seconds) if it fails at boot.
- Ignores frames that don't come from the Gateway's MAC address.
- Boots with the relay released. Power is lost whenever heating stops, which also closes the valve.

## Protocol

Every ESP-NOW frame is tagged `KHUM` (`4B 48 55 4D`, see `Common/src/HumidifierProtocol.h` and the server's `Models/FrameTags.cs`). After the tag, each frame is one message type byte followed by a versioned CBOR map (same conventions as the HMI: key `0` is the schema version `1`). Every message fits in a single frame, so there is no chunking envelope.

| Type | Name    | Direction            | Body                        |
| ---: | :------ | :------------------- | :-------------------------- |
|  `1` | `Ping`  | Humidifier -> Server | `{0: 1, 1: relayOn (bool)}` |
|  `2` | `State` | Server -> Humidifier | `{0: 1, 1: enabled (bool)}` |

The Gateway relays these frames without inspecting them. The server answers each ping immediately; an invalid ping gets no reply, and any server-side error replies off. Each ping updates the device's `LastSeenAt`.

## Server decision

`Features/Humidifiers/EvaluateHumidifier.cs` turns the humidifier on only when all of these hold, and fails safe to off otherwise:

1. The device is enabled. Unknown humidifiers register themselves in the `Devices` table on their first ping, enabled, with the default configuration.
2. The current month is within the configured season (`StartMonth`..`EndMonth`, inclusive, wrapping the year).
3. Kelvin is in control (`ControlState.Enable`) and the current call is `Heating`.
4. A cached outdoor temperature is available. The humidifier never calls the weather service itself (it would stall the gateway loop); `ThermostatService` keeps the cache warm, and a miss means off.
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
