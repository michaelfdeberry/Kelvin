## Kelvin.Server

ASP.NET Core backend for Kelvin.

## What it does

- Hosts REST APIs used by Kelvin.Client.
- Runs background services for gateway discovery, sensor ingestion, thermostat logic, and control state transitions.
- Persists data with EF Core + SQLite and serves the client app static files.

## Run locally

From `src/Kelvin.Server`:

```bash
dotnet run
```

## Kelvin.Server Entity Framework Migrations

From `src/Kelvin.Server`:

```bash
dotnet tool restore
dotnet dotnet-ef migrations add <MigrationName> --output-dir Data/Migrations
dotnet dotnet-ef database update
```

Runtime startup now applies migrations automatically with `Database.Migrate()`.
For existing databases created before migrations were enabled, startup performs a one-time baseline by creating
`__EFMigrationsHistory` and marking the current initial migration as applied, preserving existing data.

## Kelvin.Server Raspberry Pi Install

From `src/Kelvin.Server` on the gateway Raspberry Pi:

```bash
chmod +x scripts/install-pi.sh scripts/start-server.sh
./scripts/install-pi.sh
```

The install script builds the Kelvin client into `Kelvin.Server/wwwroot`, publishes the ASP.NET Core server,
copies the publish output into `/opt/kelvin/Kelvin.Server/app`, installs `systemd/kelvin-server.service`, and
enables the service. It also installs nginx as a reverse proxy, enables a Kelvin site configuration, and routes
port `80` to the local ASP.NET Core process. By default Kestrel listens on `http://127.0.0.1:5209` behind nginx.
