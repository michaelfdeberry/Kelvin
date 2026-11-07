using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Models;

namespace Kelvin.Server.Features.Hmi;

public record BroadcastHmiEnvironmentReadingRequest(EnvironmentReading Reading) : IRequest;

/// <summary>
/// Pushes the latest system-wide average reading to every registered panel, mirroring what
/// <see cref="Hubs.RealtimeHub" /> pushes to web clients - this is what keeps the panel's current
/// reading live between (far less frequent) ControlStateChanged pushes.
/// </summary>
public class BroadcastHmiEnvironmentReadingHandler(
  IHandler<GetHmiMacAddressesRequest, GetHmiMacAddressesResponse> getMacAddresses,
  IDeviceOutboundChannel outboundChannel
) : IHandler<BroadcastHmiEnvironmentReadingRequest>
{
  public async Task<Result> HandleAsync(BroadcastHmiEnvironmentReadingRequest request, CancellationToken ct = default)
  {
    var macAddressesResult = await getMacAddresses.HandleAsync(new GetHmiMacAddressesRequest(), ct);
    var macAddresses = macAddressesResult.Value!.MacAddresses;

    if (macAddresses.Count == 0)
      return Result.Success();

    var payload = HmiEnvironmentReadingEncoder.Encode(request.Reading);
    foreach (var macAddress in macAddresses)
    {
      outboundChannel.WriteHmi(macAddress, HmiMessageType.EnvironmentReadingChanged, payload);
    }

    return Result.Success();
  }
}

public class BroadcastHmiEnvironmentReadingFeature : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<BroadcastHmiEnvironmentReadingRequest>, BroadcastHmiEnvironmentReadingHandler>();
  }
}
