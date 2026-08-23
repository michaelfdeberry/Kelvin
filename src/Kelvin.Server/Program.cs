using System.Text.Json.Serialization;
using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Integration.GeoCoding;
using Kelvin.Server.Integration.Weather;
using Kelvin.Server.Services;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

const string DevelopmentCorsPolicy = "DevelopmentCors";
var developmentCorsOrigins =
  builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
  ?? ["http://localhost:5173", "https://localhost:5173", "http://127.0.0.1:5173", "https://127.0.0.1:5173"];

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
if (builder.Environment.IsDevelopment())
{
  builder.Services.AddCors(options =>
  {
    options.AddPolicy(
      DevelopmentCorsPolicy,
      policy =>
      {
        policy.WithOrigins(developmentCorsOrigins).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
      }
    );
  });
}

// Dependency Injection
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<EntityUpdateInterceptor>();
builder.Services.AddDbContext<KelvinContext>(
  (serviceProvider, options) =>
    options
      .UseSqlite(KelvinContext.ResolveSqliteConnectionString(builder.Configuration))
      .AddInterceptors(serviceProvider.GetRequiredService<EntityUpdateInterceptor>())
);
builder.Services.AddSingleton<IDispatcher, Dispatcher>();
builder.Services.AddHttpClient("OpenMeteoGeoCoding", client => client.BaseAddress = new Uri("https://geocoding-api.open-meteo.com/v1/"));
builder.Services.AddHttpClient("OpenMeteoWeather", client => client.BaseAddress = new Uri("https://api.open-meteo.com/v1/"));
builder.Services.AddSingleton<IWeatherApi, OpenMeteoWeatherApi>();
builder.Services.AddSingleton<IGeoCodingApi, OpenMeteoGeoCodingApi>();
builder.Services.AddSingleton<IRelayController, RelayController>();
builder.Services.AddHostedService<ControlService>();
builder.Services.AddHostedService<GatewayService>();
builder.Services.AddHostedService<SensingService>();
builder.Services.AddHostedService<ThermostatService>();
builder
  .Services.AddSignalR()
  .AddJsonProtocol(options =>
  {
    options.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.PayloadSerializerOptions.WriteIndented = false;
  });
builder.Services.Configure<JsonOptions>(options =>
{
  options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
  options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
  options.SerializerOptions.WriteIndented = false;
});
builder.Services.RegisterDependencies();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
  var context = scope.ServiceProvider.GetRequiredService<KelvinContext>();
  BaselineExistingDatabase(context);
  context.Database.Migrate();
}

static void BaselineExistingDatabase(KelvinContext context)
{
  var connection = context.Database.GetDbConnection();
  if (connection.State != System.Data.ConnectionState.Open)
  {
    connection.Open();
  }

  using var command = connection.CreateCommand();
  command.CommandText = """
    SELECT COUNT(1)
    FROM sqlite_master
    WHERE type = 'table'
      AND name NOT LIKE 'sqlite_%'
      AND name <> '__EFMigrationsHistory';
    """;
  var userTableCount = Convert.ToInt32(command.ExecuteScalar() ?? 0);

  command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory' LIMIT 1;";
  var hasMigrationHistoryTable = command.ExecuteScalar() is not null;

  var appliedMigrationCount = 0;
  if (hasMigrationHistoryTable)
  {
    command.CommandText = "SELECT COUNT(1) FROM \"__EFMigrationsHistory\";";
    appliedMigrationCount = Convert.ToInt32(command.ExecuteScalar() ?? 0);
  }

  if (userTableCount == 0 || appliedMigrationCount > 0)
  {
    return;
  }

  var latestMigration = context.Database.GetMigrations().LastOrDefault();
  if (string.IsNullOrWhiteSpace(latestMigration))
  {
    return;
  }

  var productVersion = typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "10.0.10";

  context.Database.ExecuteSqlRaw(
    """
    CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
      "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
      "ProductVersion" TEXT NOT NULL
    );
    """
  );

  context.Database.ExecuteSqlInterpolated(
    $"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({latestMigration}, {productVersion});"
  );
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
  app.UseCors(DevelopmentCorsPolicy);
}

if (!app.Environment.IsDevelopment())
{
  app.UseHttpsRedirection();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapEndpoints();
app.MapFallbackToFile("index.html");

app.Run();
