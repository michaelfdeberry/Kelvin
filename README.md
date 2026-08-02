# Kelvin

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

## Kelvin Simulator

The environment simulator lives in [src/Kelvin.Simulator/README.md](src/Kelvin.Simulator/README.md).
Its end-to-end gateway discovery smoke test is Phase 2 and depends on a working virtual serial port setup
being installed and configured on the host OS.
