using Kelvin.Server.Models;

namespace Kelvin.Server.Features.Devices;

public record DeviceRequest(string Name);

public record DeviceResponse(Guid Id, string Name, DeviceType Type, string? MacAddress, bool Enabled, DateTimeOffset? LastSeenAt)
{
  public static DeviceResponse FromDevice(Device device) =>
    new(
      device.Id,
      device.Name,
      device switch
      {
        HumidifierDevice => DeviceType.Humidifier,
        _ => throw new NotSupportedException($"Unsupported device type {device.GetType().Name}."),
      },
      device.MacAddress,
      device.Enabled,
      device.LastSeenAt
    );
}
