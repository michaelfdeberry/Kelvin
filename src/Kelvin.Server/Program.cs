using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Integration;
using Kelvin.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var profilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var databasePath = Path.Combine(profilePath, "kelvin", "kelvin.db");
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Dependency Injection
builder.Services.AddDbContext<KelvinContext>(options => options.UseSqlite($"Data Source={databasePath}"));
builder.Services.AddSingleton<IDispatcher, Dispatcher>();
builder.Services.AddHttpClient("OpenMeteo", client => client.BaseAddress = new Uri("https://api.open-meteo.com/v1/"));
builder.Services.AddSingleton<IWeatherApi, MeteoWeatherApi>();
builder.Services.AddSingleton<IGeoCodingApi, OpenMeteoGeoCodingApi>();
builder.Services.AddHostedService<GatewayService>();
builder.Services.AddHostedService<SensingService>();
builder.Services.AddHostedService<ControlService>();
builder.Services.RegisterDependencies();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
  var context = scope.ServiceProvider.GetRequiredService<KelvinContext>();
  context.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapEndpoints();

app.Run();
