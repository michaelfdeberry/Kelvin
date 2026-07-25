namespace Kelvin.Server.Integration.Weather;

public class WeatherException : Exception
{
  public WeatherException(string message)
    : base(message) { }

  public WeatherException(string message, Exception innerException)
    : base(message, innerException) { }
}
