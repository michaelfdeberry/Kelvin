using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Features.Devices;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Humidifiers;

/// <summary>A humidifier frame with the tag already stripped.</summary>
public record ReceiveHumidifierMessageRequest(string MacAddress, byte[] Payload) : IRequest;

public static class ReceiveHumidifierMessageErrors
{
  public static readonly Error InvalidPayload = new("ReceiveHumidifierMessage.InvalidPayload", "The humidifier message payload was invalid.");
}

/// <summary>
/// Records a humidifier's check-in or state acknowledgement, registering unknown devices by MAC. The relay state
/// it reports becomes the published device state, and a check-in prompts the server to send the current state.
/// </summary>
public class ReceiveHumidifierMessageHandler(
  KelvinContext context,
  TimeProvider time,
  IDispatcher dispatcher,
  IHandler<UpdateDeviceStateRequest> deviceStates
) : IHandler<ReceiveHumidifierMessageRequest>
{
  public async Task<Result> HandleAsync(ReceiveHumidifierMessageRequest request, CancellationToken ct = default)
  {
    if (!HumidifierProtocol.TryDecode(request.Payload, out var type, out var relayOn))
      return Result.Failure(ReceiveHumidifierMessageErrors.InvalidPayload);

    var device = await RegisterAsync(request.MacAddress, ct);
    await deviceStates.HandleAsync(new UpdateDeviceStateRequest(DeviceStateNames.Humidifier, device.Enabled, new HumidifierState(relayOn)), ct);

    // Refreshes the device list's last seen time, and the integration service answers with the current state.
    if (type == HumidifierMessageType.CheckIn)
      await dispatcher.PublishAsync(new DevicesChangedEvent(), ct);

    return Result.Success();
  }

  private async Task<HumidifierDevice> RegisterAsync(string macAddress, CancellationToken ct)
  {
    var device = await context.Devices.OfType<HumidifierDevice>().FirstOrDefaultAsync(d => d.MacAddress == macAddress, ct);
    if (device is null)
    {
      device = new HumidifierDevice
      {
        Name = "Humidifier",
        MacAddress = macAddress,
        Enabled = true,
        Configuration = new HumidifierConfiguration(),
      };
      context.Devices.Add(device);
    }
    // Mirrors the Hmi/Sensor behavior: a deleted device that reappears is restored, but left disabled.
    else if (device.DeletedAt is not null)
    {
      device.Enabled = false;
      device.DeletedAt = null;
    }

    device.LastSeenAt = time.GetUtcNow();
    await context.SaveChangesAsync(ct);
    return device;
  }
}

public class ReceiveHumidifierMessageRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<ReceiveHumidifierMessageRequest>, ReceiveHumidifierMessageHandler>();
  }
}
