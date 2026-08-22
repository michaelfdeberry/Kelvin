# Kelvin

Kelvin is a smart thermostat platform with remote sensor nodes.
It monitors temperature, CO2, and humidity across the home, then helps manage HVAC behavior from one place.

Built as a full-stack system, Kelvin combines:

- embedded sensor and gateway devices,
- a backend service for automation and data handling,
- a web app for monitoring and control,
- and a simulator for local development/testing.

The goal is simple: keep indoor comfort and air quality easier to track and control.

## Kelvin Client

The web app lives in [src/Kelvin.Client/README.md](src/Kelvin.Client/README.md).
Built with Lit + TypeScript and communicates with Kelvin.Server over REST and SignalR.

## Kelvin Devices

Device firmware lives in [src/Kelvin.Devices/README.md](src/Kelvin.Devices/README.md).
Includes ESP32 node and gateway code plus shared packet contracts.

## Kelvin Server

The backend lives in [src/Kelvin.Server/README.md](src/Kelvin.Server/README.md).
It hosts APIs, runs control/sensing services, and stores data in SQLite via EF Core.

## Kelvin Simulator

The environment simulator lives in [src/Kelvin.Simulator/README.md](src/Kelvin.Simulator/README.md).
Requires a working virtual serial port setup being installed and configured on the host OS.
