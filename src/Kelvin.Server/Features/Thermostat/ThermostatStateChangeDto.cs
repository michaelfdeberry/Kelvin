using Kelvin.Server.Models;

namespace Kelvin.Server.Features.Thermostat;

public record ThermostatStateChangeDto(Guid Id, RunMode Mode, bool FanEnabled, DateTimeOffset ChangedAt)
{
  public static ThermostatStateChangeDto FromEntity(Models.Thermostat thermostat) =>
    new(thermostat.Id, thermostat.Mode, thermostat.FanEnabled, thermostat.UpdatedAt!.Value);
}
