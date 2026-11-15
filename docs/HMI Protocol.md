# HMI Protocol

## Transport

The HMI payload migration to CBOR does not change the `KHMI` tag, serial relay, bounded latest-state
outbound queue, or chunk reassembly. Each frame still carries the shared 9-byte device envelope
(`Kelvin.Protocol/DeviceEnvelope.cs`, `Kelvin.Devices/Common/src/DeviceEnvelope.h`, shared by devices with 2-way communication):

`[type:1][chunk index:1][chunk count:1][body length:2 LE][message ID:4 LE][body]`

All chunks of one message share the same ID. The panel requires consecutive indices and consistent
IDs/counts before publishing a completed message. Only the reassembled body is decoded as a payload.
The CBOR library handles CBOR's own byte order; the envelope integers remain little-endian.

## Payloads

Every HMI payload in both directions is now CBOR schema version 1. The table lists the required fields
in addition to key `0`, which is always the version. Nullable fields must be present, using CBOR `null`
when unset. Booleans are CBOR booleans, not integer flags.

| Message                   | Type   | Field Keys                                                                                                      |
| ------------------------- | ------ | --------------------------------------------------------------------------------------------------------------- |
| ThermostatStateChunk      | `0x01` | 1 mode, 2 fan enabled, 3 hysteresis, 4 heating lockout, 5 cooling lockout, 6 set-point array, 7 schedule array  |
| SensorReading             | `0x02` | 1 temperature, 2 humidity, 3 CO2 (unsigned 16-bit range), 4 battery percentage                                  |
| ControlStateChanged       | `0x03` | 1 state, 2 environment temperature, 3 target temperature, 4 humidity, 5 CO2; keys 2-5 nullable                  |
| EnvironmentReadingChanged | `0x04` | 1 temperature, 2 humidity, 3 CO2 (float)                                                                        |
| PreferencesChanged        | `0x05` | 1 temperature unit, 2 time format                                                                               |
| Notify                    | `0x06` | 1 ID, 2 message, 3 severity, 4 dismissible, 5 heading (nullable), 6 banner, 7 duration (nullable), 8 persistent |
| DismissNotification       | `0x07` | 1 notification ID; supported in both directions                                                                 |
| SetMode                   | `0x10` | 1 mode                                                                                                          |
| SetFanEnabled             | `0x11` | 1 enabled (boolean)                                                                                             |
| SetSetPoint               | `0x12` | 1 run type, 2 target temperature                                                                                |
| UpsertSchedule            | `0x13` | 1 ID (nullable), 2 run type, 3 start minutes, 4 end minutes, 5 target temperature                               |
| RemoveSchedule            | `0x14` | 1 ID                                                                                                            |
| SetForecastLockouts       | `0x15` | 1 heating lockout, 2 cooling lockout (both nullable)                                                            |
| RequestState              | `0x16` | Version only                                                                                                    |

Set-point entries are maps `{1: run type, 2: target temperature}`. Schedule entries are maps
`{1: ID, 2: run type, 3: start minutes, 4: end minutes, 5: target temperature}`. These nested entries
inherit their parent payload's version; they have no key `0`. IDs are opaque 16-byte CBOR byte strings
in the existing `Guid.ToByteArray()` order, not UUID text or RFC network byte order.

Modes are 0-4, run types are 0-1, and control states are 0-8, matching the shared enums. Temperature units
are Celsius=0/Fahrenheit=1; time formats are Hour24=0/Hour12=1. Minutes are 0-1439. Temperatures are Celsius.
Thermostat snapshots are limited to two set points and 32 schedules, and still must fit in 2048 bytes.
Snapshots exceeding those limits are rejected rather than silently truncated. Application handlers retain
their existing thermostat safety checks after structural decoding.

Deploy the matching server and simulator and reflash all HMI panels together. Previous positional binary
payloads are not supported. There is no version negotiation or dual decoding.
Gateway and Node firmware, the web API, and kiosk HTTP payloads are unchanged.

## CBOR Conventions

The payload shapes are recorded in [hmi-payloads.cddl](hmi-payloads.cddl). The following runtime rules
supplement that structural schema:

- Each body is exactly one definite-length map, with unsigned 32-bit integer keys and no trailing bytes.
- Key `0` is a required unsigned schema version. Versions are scoped to the message type; currently `1`.
- Keys are stable identifiers, not field positions. Never reuse a key or change its meaning/type.
- Field order is insignificant. Duplicate top-level keys, including unknown keys, are rejected.
- Add optional fields under unused keys without increasing the version. Older readers skip unknown fields.
- Removing required fields, changing field types/semantics, or adding new required fields needs a new version.
- Unsupported versions are rejected rather than interpreted using an older layout.
- Readers accept CBOR half-, single-, and double-precision floats, but require the reading values to remain
  finite when converted to a 32-bit float. Integer values are not substitutes for float fields.
- Readers limit bodies to 2048 bytes, top-level maps to 32 fields, and unknown-field values to four nested
  containers. Indefinite-length items and CBOR tags are not supported. Unknown values within these limits
  are skipped without changing the interpretation of known fields.
- The panel applies state only after the complete payload passes validation; malformed commands are rejected
  by the server before registration or dispatch. Command bodies must fit in a single envelope chunk.

The server uses .NET 10's `System.Formats.Cbor`. The panel uses the allocation-free C API from
`TinyCBOR@0.5.3002`, installed by `Hmi.bat`. CBOR is self-describing but does not generate bindings from
CDDL here: changing the contract still requires updating the relevant codecs and interoperability tests.
The .NET server and simulator share the bounded codec helpers in `src/Kelvin.Protocol/HmiCbor.cs`;
the panel parsers and encoders share `Hmi/src/protocol/HmiCbor.h` over TinyCBOR.

## EnvironmentReadingChanged v1

| Key | Field             | Required | Value                         |
| --- | ----------------- | -------- | ----------------------------- |
| `0` | Schema version    | Yes      | Unsigned integer `1`          |
| `1` | Temperature       | Yes      | Finite float, degrees Celsius |
| `2` | Relative humidity | Yes      | Finite float, percentage      |
| `3` | CO2               | Yes      | Finite float, ppm             |

For temperature `21.5`, humidity `50`, and CO2 `400`, the golden body is:

`A4000101F94D6002F9524003F95E40`

This is a 15-byte map. The writer uses the shortest float representation that preserves each value
exactly; arbitrary 32-bit readings may require longer encodings. The same vector is checked in the
server/simulator tests and the native firmware tests.

## Notifications v1

Notification IDs use the same opaque 16-byte Guid encoding as schedule IDs. Severity is
Information=0, Warning=1, Success=2, Error=3. Message and heading are UTF-8 text, bounded to
1024 and 128 bytes respectively; the server shortens longer text at a complete character boundary.
Embedded NUL characters are not supported. Duration is milliseconds; null or zero means no timeout.
The persistent flag identifies a stored notification whose local close or timeout should send a
DismissNotification uplink, using the standard dismissal handler and shared dismissal event.
A dismissal push removes that ID without sending a dismissal back.

Standard notify/dismiss events are broadcast to enabled, non-deleted panels. Command failures bypass
the event system: GatewayService queues an Error notification for only the originating MAC, with
persistent=false, a close button, and a ten-second timeout. These failures are not stored and do not
appear on other panels or the website. An error-triggered state refresh restores authoritative
settings; refresh requests are rate-limited to once every five seconds.

Notify and DismissNotification messages retain FIFO ordering in the bounded outbound queue, rather
than coalescing by panel/type like state snapshots. Queue capacity still bounds pending delivery;
ESP-NOW is not an acknowledged notification transport.

The panel displays up to four bottom-right toasts and one top banner using the top LVGL layer, across
both screens. Long alerts and the toast stack scroll; newer alerts replace the oldest when full.
On touch wake it clears cached persistent alerts, and RequestState replays the latest four active
toasts plus the latest active banner. A dismissed notification is excluded from this replay.

For message "Failed", heading "Settings", severity Error, duration 10000, dismissible=true,
banner=false, persistent=false, and ID bytes 00 through 0F, the golden body is:

`A900010150000102030405060708090A0B0C0D0E0F02664661696C6564030304F5056853657474696E677306F40719271008F4`

The corresponding dismissal body is `A200010150000102030405060708090A0B0C0D0E0F`.

## Adding Messages

Define each new map in CDDL and this document, then update the relevant server, firmware, and simulator
codecs and interoperability tests. Keep transport, structural decoding, and domain validation separate.
