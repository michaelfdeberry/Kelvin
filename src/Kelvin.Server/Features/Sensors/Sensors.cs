using Kelvin.Server.Models;

namespace Kelvin.Server.Features.Sensors
{
  public record SensorRequest(
    Guid? Id,
    string Name,
    string MacAddress,
    bool HasBattery,
    bool HasHumiditySensor,
    bool HasCO2Sensor,
    float TemperatureCOffset = 0,
    float HumidityPercentageOffset = 0,
    short CO2LevelPpmOffset = 0
  );

  public record SensorResponse(
    Guid Id,
    string? Name,
    string? MacAddress,
    bool HasBattery,
    bool HasHumiditySensor,
    bool HasCO2Sensor,
    bool Enabled,
    float TemperatureCOffset = 0,
    float HumidityPercentageOffset = 0,
    short CO2LevelPpmOffset = 0,
    DateTimeOffset? LastReadingAt = null
  )
  {
    public static SensorResponse FromSensor(Sensor sensor)
    {
      return new SensorResponse(
        sensor.Id,
        sensor.Name,
        sensor.MacAddress,
        sensor.HasBattery,
        sensor.HasHumiditySensor,
        sensor.HasCO2Sensor,
        sensor.Enabled,
        sensor.TemperatureCOffset,
        sensor.HumidityPercentageOffset,
        sensor.CO2LevelPpmOffset
      );
    }
  }
}
