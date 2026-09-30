namespace Kelvin.Server.Models;

/// <summary>
/// A wall-mounted touchscreen thermostat panel, reachable through the Gateway's ESP-NOW bridge.
/// </summary>
public class Hmi : Entity
{
  public string? MacAddress { get; set; }

  public string? Name { get; set; }

  public bool Enabled { get; set; } = true;
}
