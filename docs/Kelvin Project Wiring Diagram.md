# Project Kelvin — Wiring Diagrams

Derived from [Kelvin Project Reference Guide.md](Kelvin%20Project%20Reference%20Guide.md). Everything below reflects the **intended** build as described in that guide.

This file covers system topology and the gateway enclosure. Per-device wiring lives in:

- [Kelvin Node Wiring Diagram.md](Kelvin%20Node%20Wiring%20Diagram.md) — ESP32-S3 PowerFeather V2R2, ST7789 SPI display, SHT4x
- [Kelvin HMI Wiring Diagram.md](Kelvin%20HMI%20Wiring%20Diagram.md) — Waveshare ESP32-S3-Touch-LCD-7B, shared I2C bus
- [Kelvin Kiosk Wiring Diagram.md](Kelvin%20Kiosk%20Wiring%20Diagram.md) — Raspberry Pi 3B+, SCD40

---

## 1. System topology

```mermaid
flowchart LR
    subgraph Field["Field devices"]
        N1["Node<br/>ESP32-S3 PowerFeather V2R2<br/>SHT4x + ST7789"]
        N2["Node (xN)"]
        H1["HMI<br/>Waveshare ESP32-S3-Touch-LCD-7B<br/>7in 1024x600 capacitive"]
        K1["Kiosk<br/>Raspberry Pi 3B+<br/>SCD40 + 8in touchscreen"]
    end

    subgraph Enclosure["Gateway enclosure (ABS, DIN rail)"]
        ESP["ESP32 receiver<br/>(ESP-NOW transceiver)"]
        PI["Raspberry Pi 5<br/>.NET application"]
        RB["4-channel relay bank<br/>5V coil, active low"]
    end

    WEB["Web clients<br/>/hubs/realtime"]
    HVAC["Furnace / HVAC<br/>24V AC: R, W, Y, G, C"]
    WT["Legacy wall thermostat"]

    N1 -- "ESP-NOW, KNOD frames" --> ESP
    N2 -- "ESP-NOW, KNOD frames" --> ESP
    H1 <-- "ESP-NOW two-way, KHMI frames" --> ESP
    ESP <-- "USB serial<br/>MAC + length + frame tag" --> PI
    K1 -- "HTTP POST /api/sensors/packets" --> PI
    PI <-- "SignalR" --> WEB
    PI -- "GPIO (active low)" --> RB
    RB -- "24V AC switching" --> HVAC
    RB -- "NC failsafe path" --> WT
    HVAC -- "24V AC R supply" --> Enclosure
```

Kiosks deliberately bypass the ESP32 gateway and talk straight to the server over HTTP.

---

## 2. Gateway enclosure — DC power and logic

```mermaid
flowchart TD
    MAINS["120V AC mains"] --> PSU["Mean Well DIN PSU<br/>5V DC 40A"]

    PSU -- "5V / GND" --> PI["Raspberry Pi 5"]
    PSU -- "5V switched by DPDT pole 2" --> JDVCC["Relay bank JD-VCC<br/>(coil supply)"]

    PI -- "USB-A to USB" --> ESP["ESP32 receiver"]
    PI -- "3.3V logic VCC + GND" --> RLOGIC["Relay bank VCC / GND<br/>(opto input side)"]

    PI -- "ControlPin -> IN4" --> R4["Relay 4 — System Control Failsafe"]
    PI -- "HeatingPin -> IN2" --> R2["Relay 2 — Heat (W)"]
    PI -- "CoolingPin -> IN1" --> R1["Relay 1 — Cool (Y)"]
    PI -- "FanPin -> IN3" --> R3["Relay 3 — Fan (G)"]

    JDVCC --> R1
    JDVCC --> R2
    JDVCC --> R3
    JDVCC --> R4
```

**Notes**

- The relay board is **active low**: the server drives a pin **LOW** to energize a relay, **HIGH** to release it (`RelayController`).
- GPIO assignments are not hard-coded. `HeatingPin`, `CoolingPin`, `FanPin`, and `ControlPin` live in the `Gateway` record in the database and are applied at runtime, so record the actual BCM numbers used on the build.
- Remove the **VCC–JD-VCC jumper** on the relay board so the opto-isolator input side is powered from the Pi while the coil side is powered from the PSU through the DPDT switch. This is what lets the "Legacy Override" position fully disable the relays.
- The Pi's GND and the PSU GND must be common for the GPIO signals to be referenced correctly.

---

## 3. Gateway enclosure — 24V AC HVAC control path

This is the tiered control system: DPDT manual override **in series with** the software-controlled Relay 4.

```mermaid
flowchart TD
    FR["Furnace R<br/>24V AC hot"] --> F1["3A fuse<br/>(incoming)"]
    F1 --> DP["DPDT switch<br/>pole 1 common"]

    DP -- "Legacy Override" --> WTR
    DP -- "Auto" --> R4C["Relay 4 COM"]

    DP2["DPDT pole 2<br/>(5V from PSU)"] -- "Legacy Override: open" --> X["JD-VCC disconnected<br/>relay bank disabled"]
    DP2 -- "Auto: closed" --> JD["JD-VCC connected<br/>relay bank armed"]

    R4C --> R4NC["Relay 4 NC<br/>(de-energized = default)"]
    R4C --> R4NO["Relay 4 NO<br/>(energized by .NET app)"]

    R4NC --> WTR["Legacy wall thermostat R"]
    R4NO --> BUS["Shared 24V AC bus to<br/>Relay 1/2/3 COM"]

    BUS --> R1["Relay 1 COM — Cool"]
    BUS --> R2["Relay 2 COM — Heat"]
    BUS --> R3["Relay 3 COM — Fan"]

    R1 -- "NO" --> FY["3A fuse -> Furnace Y"]
    R2 -- "NO" --> FW["3A fuse -> Furnace W"]
    R3 -- "NO" --> FG["3A fuse -> Furnace G"]

    WTR --> WTOUT["Wall thermostat W / Y / G outputs"]
    WTOUT --> FURN["Furnace control board"]
    FY --> FURN
    FW --> FURN
    FG --> FURN
    FC["Furnace C — 24V AC common"] --> FURN
```

**Failsafe behavior**

| Condition                                      | Result                                                                                                                 |
| :--------------------------------------------- | :--------------------------------------------------------------------------------------------------------------------- |
| DPDT in **Legacy Override**                    | 24V AC routed straight to the wall thermostat; JD-VCC cut, relay bank completely dead.                                 |
| DPDT in **Auto**, Relay 4 de-energized         | Relay 4 NC hands 24V AC to the wall thermostat. This is the state after a Pi crash, power loss, or thermostat Disable. |
| DPDT in **Auto**, Relay 4 energized (GPIO LOW) | Relay 4 NO feeds the COM of Heat/Cool/Fan; Kelvin owns the HVAC.                                                       |

Relays 1–3 can only pass current when both the DPDT switch is in Auto **and** Relay 4 is energized, so there is no path for software to call heat while the user has chosen manual override.

---

## 4. Indicator lights (24V AC)

```mermaid
flowchart LR
    R4NO["Relay 4 NO terminal"] --> GREEN["GREEN LED<br/>Gateway Active"]
    WTR["R at the legacy wall thermostat"] --> RED["RED LED<br/>Legacy Thermostat Active"]
    W["HVAC W (Heat)"] --> ORANGE["ORANGE LED<br/>Heating Active"]
    Y["HVAC Y (Cool)"] --> BLUE["BLUE LED<br/>Cooling Active"]
    C["HVAC C (Common)"] --> ORANGE
    C --> BLUE
    C --> GREEN
    C --> RED
```

| LED    | Tap point                                                     | Lit when                                                                   |
| :----- | :------------------------------------------------------------ | :------------------------------------------------------------------------- |
| Green  | Relay 4 **NO** terminal, referenced to C                      | DPDT in Auto **and** the .NET app has seized control                       |
| Red    | R wire where it enters the legacy thermostat, referenced to C | Either the DPDT switch or Relay 4 has handed control back to the wall unit |
| Orange | Parallel across W and C                                       | Relay 2 bridges R→W                                                        |
| Blue   | Parallel across Y and C                                       | Relay 1 bridges R→Y                                                        |

Green and Red are mutually exclusive — they are the two sides of the Relay 4 transfer contact.

---

## 5. Enclosure conductor and connector rules

```mermaid
flowchart LR
    subgraph OUT["Outside the box"]
        FURN2["Furnace"]
        WT2["Wall thermostat"]
    end
    subgraph BOX["Enclosure"]
        TB["DIN terminal blocks"]
        INT["Relay bank, PSU, Pi, ESP32"]
        subgraph DOOR["Door-mounted"]
            LEDS["4x indicator LEDs"]
            SW["DPDT override switch"]
        end
    end

    FURN2 -- "solid core" --> TB
    WT2 -- "solid core" --> TB
    TB -- "stranded, silicone jacket" --> INT
    INT -- "stranded, silicone jacket" --> LEDS
    INT -- "stranded, silicone jacket" --> SW
```

- **Solid core**, exclusively, for furnace entry lines and wall-thermostat exit lines, landed directly on dedicated DIN terminals.
- **Stranded copper with silicone insulation** for all internal routing, for flex life and to avoid metal fatigue.
- 3A fuses on the incoming 24V AC line and on each outgoing 24V AC line back to the furnace.

---

## 6. Signal flow summary

```mermaid
sequenceDiagram
    participant S as Sensor device
    participant G as ESP32 gateway
    participant P as Pi 5 (.NET)
    participant R as Relay bank
    participant F as Furnace

    S->>G: ESP-NOW frame (KNOD / KHMI)
    G->>P: USB serial: MAC + length + tag + payload
    P->>P: SensingService averages enabled sensors
    P->>P: ThermostatService evaluates mode/schedule/setpoint/hysteresis
    P->>R: ControlService: ControlPin LOW (seize control)
    P->>R: HeatingPin / CoolingPin / FanPin LOW
    R->>F: 24V AC R -> W / Y / G
    P-->>G: Downlink HMI config + call state
    G-->>S: ESP-NOW relay to addressed MAC
```
