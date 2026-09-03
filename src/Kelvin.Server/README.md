## Kelvin.Server

ASP.NET Core backend for Kelvin.

## What it does

- Hosts REST APIs used by Kelvin.Client.
- Runs background services for gateway discovery, sensor ingestion, thermostat logic, and control state transitions.
- Persists data with EF Core + SQLite and serves the client app static files.

## REPR vertical slices

API features use the REPR pattern: each vertical slice keeps its **Request**, endpoint **Endpoint**, business **Handler**, and API **Response** together in one feature file under `Features/<FeatureGroup>`. This keeps a feature's HTTP mapping, request/response contract, implementation, error definitions, and dependency-injection registration close together instead of splitting them by technical layer.

To create a slice, create a C# file in the relevant `Features` folder, type one of the following prefixes, and select the VS Code snippet:

- `vslice` creates an endpoint with a request and response.
- `vslice-paged` creates a paged endpoint with query-string page options.
- `vslice-no-response` creates an endpoint that returns no response body.

Use Tab to fill in the feature group, feature name, request/response properties, route, and endpoint tag. Then add or remove code as needed. E.g. replace the generated TODO handler logic, set the appropriate HTTP method, default error with feature-specific behavior. The snippets are defined in `.vscode/dotnet.code-snippets`.

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

1. On the Raspberry Pi (running Raspberry Pi OS Lite), clone the repo:
   `git clone https://github.com/michaelfdeberry/Kelvin.git`
2. `cd Kelvin/src/Kelvin.Server` and run `./scripts/install-pi.sh`.

The install script builds the Kelvin client into `Kelvin.Server/wwwroot`, publishes the ASP.NET Core server,
copies the publish output into `/opt/kelvin/Kelvin.Server/app`, installs `systemd/kelvin-server.service`, and
enables the service. It also installs nginx as a reverse proxy, enables a Kelvin site configuration, and routes
port `80` to the local ASP.NET Core process. By default Kestrel listens on `http://127.0.0.1:5209` behind nginx.
