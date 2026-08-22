# Kelvin.Client

Web UI for monitoring and controlling Kelvin from the browser.

## What it does

- Renders the dashboard, analytics, and settings views.
- Fetches thermostat, sensor, and preference data from Kelvin.Server.
- Subscribes to SignalR updates for live control/readings state.

## Stack

- Lit + TypeScript
- Vite for local development and build
- pnpm for package management

## Common scripts

From `src/Kelvin.Client`:

```bash
pnpm install
pnpm dev
pnpm build
pnpm lint
```
