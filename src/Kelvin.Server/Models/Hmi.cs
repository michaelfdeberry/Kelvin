using System.ComponentModel.DataAnnotations.Schema;

namespace Kelvin.Server.Models;

/// <summary>
/// A wall-mounted touchscreen thermostat panel, reachable through the Gateway's ESP-NOW bridge.
/// </summary>
public class Hmi : Entity
{
  public string? MacAddress { get; set; }

  // The panel's onboard sensor, which owns its name and enabled state.
  public Guid? SensorId { get; set; }

  [ForeignKey(nameof(SensorId))]
  public virtual Sensor? Sensor { get; set; }
}
