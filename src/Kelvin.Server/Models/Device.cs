namespace Kelvin.Server.Models;

public enum DeviceType
{
  Gateway,
  Node,
}

public class Device : Entity
{
  public string? Name { get; set; }

  public string? MacAddress { get; set; }

  public DeviceType? Type { get; set; }
}
