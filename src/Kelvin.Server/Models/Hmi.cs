using System.ComponentModel.DataAnnotations.Schema;

namespace Kelvin.Server.Models;

/// <summary>
/// A wall-mounted touchscreen thermostat panel, reachable through the Gateway's ESP-NOW bridge.
/// </summary>
public class Hmi : Entity
{
  public string? MacAddress { get; set; }

  public string? Name { get; set; }

  public bool Enabled { get; set; } = true;

  // The panel's own onboard temperature/humidity sensor - its Name is managed through this Sensor, not set
  // directly on the Hmi (see UpdateSensorHandler).
  public Guid? SensorId { get; set; }

  [ForeignKey(nameof(SensorId))]
  public virtual Sensor? Sensor { get; set; }
}
