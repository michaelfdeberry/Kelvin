# Kelvin Kiosk — Wiring Diagram

Raspberry Pi 3B+ kiosk with CO2 sensing and a local UI. Part of the [Kelvin Project Wiring Diagram.md](Kelvin%20Project%20Wiring%20Diagram.md) set.

---

## Wiring

```mermaid
flowchart LR
    PI3["Raspberry Pi 3B+"]
    SCD["Sensirion SCD40<br/>CO2 / temp / humidity"]
    TS["8in touchscreen"]
    NET["LAN / Wi-Fi"]

    PI3 -- "3V3 + GND" --> SCD
    PI3 -- "I2C: SDA / SCL" --- SCD
    PI3 --- TS
    PI3 -- "HTTP POST /api/sensors/packets" --> NET
```

## Notes

- Mains-powered, so there is no battery telemetry to wire or report.
- The SCD40 is the only CO2 source in the system — Nodes and HMIs report 0 for that field.
- Kiosks **do not** go through the ESP32 gateway. They post sensor packets straight to the server over HTTP, so the only "link" to wire is network connectivity.
