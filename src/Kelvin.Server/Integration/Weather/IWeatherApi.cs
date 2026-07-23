using Kelvin.Server.Models;

namespace Kelvin.Server.Integration.Weather;

public interface IWeatherApi
{
  Task<WeatherForecast?> GetForecastAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}
