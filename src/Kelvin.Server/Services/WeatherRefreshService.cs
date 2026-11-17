using Kelvin.Server.Application;
using Kelvin.Server.Features.GeoCoding;
using Kelvin.Server.Features.Weather;

namespace Kelvin.Server.Services;

/// <summary>
/// Keeps the weather forecast fresh so it is pushed to clients as it changes, rather than only being fetched when something asks for it.
/// </summary>
/// <remarks>
/// The forecast is cached, and the handler only fetches (and pushes) once the cache has expired, so asking more often than the cache
/// lifetime just means the new forecast is picked up soon after it expires.
/// </remarks>
public class WeatherRefreshService(ILogger<WeatherRefreshService> logger, TimeProvider time, IDispatcher dispatcher) : BackgroundService
{
  public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(15);

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        var result = await dispatcher.DispatchAsync<GetWeatherForecastRequest, GetWeatherForecastResponse>(new(), stoppingToken);

        // Not having a location is an acceptable reason not to have a forecast.
        if (result.IsFailure && result.Error != GetCurrentLocationErrors.LocationNotConfigured)
          logger.LogWarning("Failed to refresh the weather forecast: {Error}", result.Error);

        await Task.Delay(RefreshInterval, time, stoppingToken);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        break;
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "An error occurred while refreshing the weather forecast.");
        await Task.Delay(RefreshInterval, time, stoppingToken);
      }
    }
  }
}
