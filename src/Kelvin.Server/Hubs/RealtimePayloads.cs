using Kelvin.Server.Features.Devices;
using Kelvin.Server.Features.Humidifiers;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Models;

namespace Kelvin.Server.Hubs;

/// <summary>The thermostat and everything a client needs to show and edit it, pushed together.</summary>
public record ThermostatSettingsPayload(
  GetThermostatResponseDto Thermostat,
  IEnumerable<SetPointResponse> SetPoints,
  IEnumerable<ScheduleResponse> Schedules
)
{
  /// <summary>Expects the thermostat's set points and schedules to be loaded.</summary>
  public static ThermostatSettingsPayload FromEntity(Models.Thermostat thermostat) =>
    new(
      GetThermostatResponseDto.FromThermostat(thermostat),
      thermostat.SetPoints.Where(s => s.DeletedAt is null).Select(SetPointResponse.FromEntity).ToList(),
      thermostat.Schedules.Where(s => s.DeletedAt is null).Select(ScheduleResponse.FromEntity).ToList()
    );
}

/// <summary>The devices and the per-type configuration that lives alongside them, pushed together.</summary>
public record DevicesSettingsPayload(List<DeviceResponse> Devices, List<HumidifierResponse> Humidifiers);

/// <summary>One created or changed device, with the per-type configuration that lives alongside it.</summary>
public record DeviceChangedPayload(DeviceResponse Device, HumidifierResponse? Humidifier)
{
  public static DeviceChangedPayload FromEntity(Device device) =>
    new(DeviceResponse.FromDevice(device), device is HumidifierDevice humidifier ? HumidifierResponse.FromDevice(humidifier) : null);
}
