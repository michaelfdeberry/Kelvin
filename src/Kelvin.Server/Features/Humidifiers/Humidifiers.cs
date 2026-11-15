using Kelvin.Server.Models;

namespace Kelvin.Server.Features.Humidifiers;

public record HumidifierConfigurationRequest(int StartMonth, int EndMonth, List<HumidityTarget> Targets);

public record HumidifierResponse(Guid Id, string Name, bool Enabled, int StartMonth, int EndMonth, List<HumidityTarget> Targets)
{
  public static HumidifierResponse FromDevice(HumidifierDevice device)
  {
    var configuration = device.Configuration;
    return new HumidifierResponse(
      device.Id,
      device.Name,
      device.Enabled,
      configuration.StartMonth,
      configuration.EndMonth,
      configuration.Targets.OrderBy(target => target.OutdoorTemperatureC).ToList()
    );
  }
}
