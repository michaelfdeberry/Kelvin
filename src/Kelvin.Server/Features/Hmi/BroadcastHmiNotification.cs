using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Models;

namespace Kelvin.Server.Features.Hmi;

public record BroadcastHmiNotificationRequest(Notification Notification) : IRequest;

public record BroadcastHmiDismissNotificationRequest(Guid NotificationId) : IRequest;

public class BroadcastHmiNotificationHandler(
  IHandler<GetHmiMacAddressesRequest, GetHmiMacAddressesResponse> getMacAddresses,
  IHmiOutboundChannel outboundChannel
) : IHandler<BroadcastHmiNotificationRequest>, IHandler<BroadcastHmiDismissNotificationRequest>
{
  public Task<Result> HandleAsync(BroadcastHmiNotificationRequest request, CancellationToken ct = default) =>
    BroadcastAsync(HmiMessageType.Notify, HmiNotificationEncoder.Encode(request.Notification), ct);

  public Task<Result> HandleAsync(BroadcastHmiDismissNotificationRequest request, CancellationToken ct = default) =>
    BroadcastAsync(HmiMessageType.DismissNotification, HmiNotificationEncoder.EncodeDismiss(request.NotificationId), ct);

  private async Task<Result> BroadcastAsync(HmiMessageType type, byte[] payload, CancellationToken ct)
  {
    var macAddressesResult = await getMacAddresses.HandleAsync(new GetHmiMacAddressesRequest(), ct);
    var macAddresses = macAddressesResult.Value!.MacAddresses;

    foreach (var address in macAddresses)
    {
      outboundChannel.Write(address, type, payload);
    }

    return Result.Success();
  }
}

public class BroadcastHmiNotificationRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<BroadcastHmiNotificationRequest>, BroadcastHmiNotificationHandler>();
    services.AddScoped<IHandler<BroadcastHmiDismissNotificationRequest>, BroadcastHmiNotificationHandler>();
  }
}
