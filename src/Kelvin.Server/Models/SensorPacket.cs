using System.ComponentModel.DataAnnotations.Schema;

namespace Kelvin.Server.Models;

public class SensorPacket : Entity
{
  public string MacAddress { get; set; } = string.Empty;

  public float Temperature { get; set; }

  public float Humidity { get; set; }

  public ushort CO2Level { get; set; }

  public float BatteryLevel { get; set; }

  public Guid SensorId { get; set; }

  [ForeignKey(nameof(SensorId))]
  public virtual Sensor? Sensor { get; set; }

  public override string ToString()
  {
    return $"MAC={MacAddress}, Temp={Temperature:F2}, Humidity={Humidity:F2}, CO2={CO2Level}, Battery={BatteryLevel:F2}";
  }
}
