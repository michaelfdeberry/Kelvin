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
