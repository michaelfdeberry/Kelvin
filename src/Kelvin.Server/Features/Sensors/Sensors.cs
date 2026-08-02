using Kelvin.Server.Models;

namespace Kelvin.Server.Features.Sensors
{
  public record SensorRequest(Guid? Id, string Name, string MacAddress, bool HasBattery, bool HasHumiditySensor, bool HasCO2Sensor);

  public record SensorResponse(Guid Id, string? Name, string? MacAddress, bool HasBattery, bool HasHumiditySensor, bool HasCO2Sensor, bool Enabled)
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
        sensor.Enabled
      );
    }
  }
}
