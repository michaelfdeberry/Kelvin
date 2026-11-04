using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Models;

namespace Kelvin.Server.Features.Hmi;

public record BroadcastHmiPreferencesRequest(Models.Preferences Preferences) : IRequest;

public static class BroadcastHmiPreferencesErrors
{
  public static readonly Error DefaultError = new("BroadcastHmiPreferences.Failed", "An error occurred processing the request.");
}

public class BroadcastHmiPreferencesHandler(
  IHandler<GetHmiMacAddressesRequest, GetHmiMacAddressesResponse> getMacAddresses,
  IHmiOutboundChannel outboundChannel
) : IHandler<BroadcastHmiPreferencesRequest>
{
  public async Task<Result> HandleAsync(BroadcastHmiPreferencesRequest request, CancellationToken ct = default)
  {
    var macAddressesResult = await getMacAddresses.HandleAsync(new GetHmiMacAddressesRequest(), ct);
    var macAddresses = macAddressesResult.Value!.MacAddresses;

    if (macAddresses.Count == 0)
      return Result.Success();

    var preferences = request.Preferences;
    if (preferences is null)
      return Result.Success();

    var payload = HmiPreferencesEncoder.Encode(preferences);
    foreach (var macAddress in macAddresses)
    {
      outboundChannel.Write(macAddress, HmiMessageType.PreferencesChanged, payload);
    }

    return Result.Success();
  }
}

public class BroadcastHmiPreferencesRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<BroadcastHmiPreferencesRequest>, BroadcastHmiPreferencesHandler>();
  }
}
