# **Project Kelvin Reference Guide**

**Project Goal:** To collect temperature and humidity data from multiple locations around the house to maintain a better average temperature and gain granular control over furnace operation.  
**Architecture Overview:** Nodes and HMIs communicate with the Raspberry Pi server through an ESP32 gateway over ESP-NOW and USB serial. Kiosks submit readings directly to the server over HTTP. The server aggregates sensor readings, evaluates thermostat behavior, controls HVAC relays, and publishes real-time updates to web clients and HMIs.

**Verification note:** Software behavior below was checked against the current repository on 2026-10-03. Enclosure wiring, fuse ratings, supply ratings, and indicator-light wiring describe the intended physical build and cannot be verified from firmware or server code; check those against the as-built enclosure.

## **1\. The Nodes**

The nodes are distributed ESP32-based devices responsible for environmental data collection and local display.

### **Core Hardware**

> - **Microcontroller:** ESP32-S3 PowerFeather V2R2.
> - **Communication:** ESP-NOW (Node to Gateway).

### **Sensors**

> - **Temperature & Humidity:** Sensirion SHT4X (I2C). Selected for I2C stability and low power consumption on battery nodes.

### **Display & Power**

> - **Display:** 2" ST7789 LCD (SPI) using the configured Node pins: SCLK 16, MOSI 6, DC 17, CS 12, reset 18, and backlight 11.
> - **Input:** A context button that, when pressed briefly, wakes the device and powers on the screen. When long pressed it will toggle the display temperature unit between Celsius and Fahrenheit.
> - **Battery Monitoring:** Uses the built in ESP32-S3 PowerFeather battery monitoring.

### **Development & Environment**

> - **Libraries:** Standard Arduino libraries managed via Arduino CLI.

### **Transmission Logic**

> - **Telemetry publishing:** The firmware reads on a 60-second cadence and sends when a value changes enough or the 5-minute heartbeat expires.

**Delta Triggers**

| Metric                 | Delta   |
| :--------------------- | :------ |
| **Temperature**        | ± 0.5°C |
| **Humidity**           | ± 1.0%  |
| **Battery Percentage** | ± 5%    |

The Node has no CO2 sensor; its firmware does not use a CO2 change trigger.

## **2\. The HMIs (Human-Machine Interfaces)**

The HMIs serve as interactive, battery-powered wall touchpoints. They bridge the gap between low-power ambient sensing and two-way control over the HVAC system via the gateway.

### **Core Hardware**

> - **Microcontroller & Display:** Waveshare ESP32-S3-Touch-LCD-7B (ESP32-S3 integrated with a 7" 1024×600 IPS capacitive touch panel).
> - **Communication:** Two-way ESP-NOW between Hmi and Gateway. The gateway relays frames between the panel and server.

### **Sensors**

> - **Temperature & Humidity:** Sensirion SHT40 on the board's I2C port. It shares the same bus as the IO extension helper chip (SDA 8, SCL 9, 400 kHz) at I2C address 0x44.

### **Display & Power**

> - **Interface:** High-resolution capacitive touch panel running LVGL (or similar) for interacting with the thermostat system UI.
> - **Power & Battery:** The board provides a 3.7V single-cell battery socket.
> - **Battery Monitoring:** Firmware reads the helper chip's ADC over I2C and estimates percentage linearly between 3.2V and 4.2V; it is not a fuel-gauge reading. External power is inferred from voltage (at least 4.15V), not read from a dedicated status signal.

### **Transmission Logic**

> - **Outbound telemetry:** Hmi firmware polls every 60 seconds and sends temperature, humidity, and battery readings on change or at the 5-minute heartbeat. The wire format includes a CO2 field, but the firmware sends 0 because this panel has no CO2 sensor.
> - **Two-way interaction:** The panel sends mode, fan, setpoint, schedule, and forecast-lockout changes upstream. It receives thermostat configuration, HVAC call state, and system-wide average readings from the server. Hmi messages use a shared chunk-capable envelope over the generic gateway relay.

## **3\. The Kiosks**

The kiosks act as independent client nodes providing a local user interface and handling power-intensive environmental monitoring.  
_(Kiosks do not communicate through the ESP32 Gateway; they submit readings directly to the server over HTTP.)_

### **Core Hardware**

> - **Microcontroller:** Raspberry Pi 3B+.
> - **Display:** 8" Touchscreen.

>

### **Sensors**

> - **CO2, Temperature, & Humidity:** Sensirion SCD40 (I2C).

The kiosk posts sensor packets to `POST /api/sensors/packets`; it has no battery telemetry.

>

## **4\. The Gateway (Hardware)**

The gateway serves as the central brain and physical actuator for the HVAC system. It is housed in an ABS electrical box with DIN rail mounting.

### **Control & Power**

> - **Microcontroller:** Raspberry Pi 5\.
> - **Receiver:** ESP32 connected via USB serial to ingest ESP-NOW data and transmit outbound two-way HMI updates.
> - **DC Power:** 5V 40A DC Mean Well DIN mount power supply (Powers the Pi and Relay Bank).

### **Wiring & Connectors**

> - **External Connections:** Solid core wire is utilized exclusively for the entry lines from the furnace and the exit lines to the existing wall thermostat. These connections run directly to dedicated DIN terminals.
> - **Internal Wiring:** The remainder of the enclosure's internal routing is wired using stranded copper wire with silicone casing to ensure flexibility and prevent metal fatigue.

### **Actuation & HVAC Interface**

> - **Relays:** 4-Channel generic relay bank (5V coil, triggered via Pi GPIO).
> - **Relay 1:** Cool (Y).
> - **Relay 2:** Heat (W).
> - **Relay 3:** Fan (G).
> - **Relay 4:** System Control Failsafe.
> - **HVAC Power:** 24V AC originating from the furnace.
> - **Protection:** 3A fuses on the incoming 24V AC line and on the outgoing 24V AC lines returning to the furnace.

>

### **Tiered Control System (Manual Override & Software Failsafe)**

The system utilizes a physical DPDT switch in series with a software-controlled relay (Relay 4\) to provide a robust, failsafe mechanism for routing 24V AC power and isolating the relay logic board.

> 1. **Manual Override (DPDT Switch):** Receives 24V AC (R) from the furnace.
>
> - **Legacy Override Position:** Routes 24V AC directly to the wall thermostat, bypassing the Pi entirely. The JD-VCC power to the relay bank is cut, entirely disabling the relays.
> - **Auto Mode Position:** Routes 24V AC into the COM port of the System Control Relay (Relay 4). The JD-VCC power is connected to the relay bank, enabling software actuation.
>
> 2. **Software Failsafe (Relay 4):**

- **Normally Closed (NC):** Routes 24V AC to the legacy wall thermostat. If the Pi crashes or loses power, control instantly snaps back to the legacy thermostat.
- **Normally Open (NO):** Routes 24V AC to the COM ports of the Heat, Cool, and Fan relays. The .NET application must pull this relay's GPIO pin LOW to seize control of the HVAC.

### **Indicator Lights (24V AC)**

Indicator LEDs provide hardware-level status verification.

| LED Color  | Function                 | Wiring Condition (24V AC)                                                                                                                                               |
| :--------- | :----------------------- | :---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Green**  | Gateway Active           | Wired to the NO terminal of the System Control Relay. Illuminates only when the DPDT switch is in "Auto" AND the .NET app has actively seized control.                  |
| **Red**    | Legacy Thermostat Active | Wired to the R wire exactly where it enters the legacy thermostat. Illuminates if either the physical switch or the software relay hands control back to the wall unit. |
| **Orange** | Heating Active           | Wired in parallel across the HVAC W (Heat) and C (Common) lines. Illuminates when the relay bridges R to W.                                                             |
| **Blue**   | Cooling Active           | Wired in parallel across the HVAC Y (Cool) and C (Common) lines. Illuminates when the relay bridges R to Y.                                                             |

## **5\. The Gateway (Software)**

The software stack handles data ingestion, logical processing, hardware actuation, and equipment protection.

### **ESP32 Receiver Logic**

> - **Role:** Relays ESP-NOW frames between devices and the Pi without interpreting device types or payloads. Serial frames carry the device MAC, payload length, and device frame tag; downlink frames are relayed to the addressed device.
> - **Device dispatch:** The server interprets the four-byte frame tag (`KNOD` for Node and `KHMI` for Hmi). Hmi application messages then use their own five-byte envelope for message type, chunk index/count, and body length.

>

### **Raspberry Pi Logic**

> - **Application:** Custom .NET application running on the Pi.
> - **Data Ingestion:** Reads Node and Hmi frames via the gateway's USB serial connection. Kiosks post sensor packets directly to the server over HTTP.
> - **ThermostatService:** Evaluates system-wide averaged readings against modes, schedules, setpoints, hysteresis, and (when configured) outdoor forecast lockouts. Event handlers send Hmi configuration, call-state, and live-average updates through the gateway; browser clients receive real-time events on `/hubs/realtime`.
> - **Dynamic Sensor Averaging:** Averages enabled sensors with current readings. Readings older than 15 minutes are removed from the average and the sensor is disabled; the UI may show a sensor offline after 5 minutes. When some sensors remain, the average continues using those sensors.
> - **ControlService & Equipment Protection:** Manages the physical actuation of the 4-Channel relay bank via GPIO pins with built-in hardware protection rules:

- **Hysteresis:** Default is 0.6°C. Configured values from 0.3°C through 2.0°C are accepted; values outside that range fall back to 0.6°C. Heating and cooling thresholds are evaluated around the target using this value.
- **Minimum call duration:** Heating or cooling must remain on for at least 3 minutes before a requested transition to Dwell is applied. An earlier Dwell request is delayed unless another call cancels it.
- **Minimum off duration:** A new heating or cooling call is blocked until the system has been in Dwell for 5 minutes.
- **Mode-switch delay:** Switching from the last heating call to cooling, or the last cooling call to heating, is blocked for 15 minutes after that call ends. These timing values are enforced in software and are not read from the Gateway's legacy duration settings.
- **Zero-sensor emergency shutdown:** When all sensor readings are gone, the server disables the thermostat, turns the fan off, and sends Disable to ControlService. This releases the HVAC call relays and relinquishes the control relay to its normally closed legacy-thermostat path; it does not guarantee that the HVAC equipment remains off if the legacy thermostat calls for heat or cooling.
