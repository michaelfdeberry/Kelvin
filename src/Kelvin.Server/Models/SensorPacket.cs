using System.ComponentModel.DataAnnotations.Schema;

namespace Kelvin.Server.Models;

public class SensorPacket : Entity
{
  public string MacAddress { get; set; } = string.Empty;

  public float TemperatureC { get; set; }

  public float HumidityPercentage { get; set; }

  public ushort CO2LevelPpm { get; set; }

  public float? BatteryLevelPercentage { get; set; }

  public Guid? SensorId { get; set; }

  [ForeignKey(nameof(SensorId))]
  public virtual Sensor? Sensor { get; set; }

  public override string ToString()
  {
    var battery = BatteryLevelPercentage is null ? "n/a" : $"{BatteryLevelPercentage:F2}";
    return $"MAC={MacAddress}, Temp={TemperatureC:F2}, Humidity={HumidityPercentage:F2}, CO2={CO2LevelPpm}, Battery={battery}";
  }
}
