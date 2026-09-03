namespace Kelvin.Server.Models;

public class Sensor : Entity
{
  public bool Enabled { get; set; } = true;

  public string? Name { get; set; }

  public string? MacAddress { get; set; }

  public bool HasBattery { get; set; }

  public bool HasCO2Sensor { get; set; }

  public bool HasHumiditySensor { get; set; }

  public float TemperatureCOffset { get; set; } = 0;

  public float HumidityPercentageOffset { get; set; } = 0;

  public short CO2LevelPpmOffset { get; set; } = 0;
}
