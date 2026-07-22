using Kelvin.Server.Channels;
using Kelvin.Server.Data;
using Kelvin.Server.Gateways;
using Kelvin.Server.Sensors;
using Kelvin.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var profilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var databasePath = Path.Combine(profilePath, "kelvin", "kelvin.db");
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// dependencies
builder.Services.AddScoped<IGatewayManager, GatewayManager>();
builder.Services.AddScoped<ISensorsManager, SensorsManager>();

builder.Services.AddHostedService<GatewayService>();

builder.Services.AddSingleton<ISensorPacketChannel, SensorPacketChannel>();

builder.Services.AddDbContext<KelvinContext>(options => options.UseSqlite($"Data Source={databasePath}"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[] { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" };

app.MapGet(
    "/weatherforecast",
    () =>
    {
      var forecast = Enumerable
        .Range(1, 5)
        .Select(index => new WeatherForecast(
          DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
          Random.Shared.Next(-20, 55),
          summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
      return forecast;
    }
  )
  .WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
  public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
