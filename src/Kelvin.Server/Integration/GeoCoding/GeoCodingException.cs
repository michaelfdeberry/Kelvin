namespace Kelvin.Server.Integration.GeoCoding;

public class GeoCodingException : Exception
{
  public GeoCodingException(string message)
    : base(message) { }

  public GeoCodingException(string message, Exception innerException)
    : base(message, innerException) { }
}
