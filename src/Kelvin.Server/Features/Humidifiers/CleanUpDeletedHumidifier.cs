using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Devices;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;

namespace Kelvin.Server.Features.Humidifiers;

/// <summary>
/// A deleted humidifier drops out of <see cref="SyncHumidifierHandler" />, so nothing would ever tell it to stop.
/// Queues an off state for it, and forgets its reported state so it no longer shows as running.
/// </summary>
public class CleanUpDeletedHumidifierHandler(IDeviceOutboundChannel outboundChannel, IHandler<UpdateDeviceStateRequest> deviceStates)
  : IEventHandler<DeviceDeletedEvent>
{
  public async Task HandleAsync(DeviceDeletedEvent @event, CancellationToken cancellationToken = default)
  {
    if (@event.Device is not HumidifierDevice { MacAddress: { } macAddress })
      return;

    // Sent whether or not it was last reported running, since an acknowledgement may have been lost.
    outboundChannel.Write(macAddress, FrameTags.Humidifier, (byte)HumidifierMessageType.State, HumidifierProtocol.EncodeState(false));

    await deviceStates.HandleAsync(
      new UpdateDeviceStateRequest(DeviceStateNames.Humidifier, DeviceEnabled: false, new HumidifierState(false)),
      cancellationToken
    );
  }
}

public class CleanUpDeletedHumidifierRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IEventHandler<DeviceDeletedEvent>, CleanUpDeletedHumidifierHandler>();
  }
}
