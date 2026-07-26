namespace Kelvin.Server.Services;

public class GpioUnavailableException : Exception
{
  public GpioUnavailableException(string message)
    : base(message) { }

  public GpioUnavailableException(string message, Exception innerException)
    : base(message, innerException) { }
}
