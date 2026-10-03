using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Hmi;

public record BroadcastHmiEnvironmentReadingRequest(EnvironmentReading Reading) : IRequest;

/// <summary>
/// Pushes the latest system-wide average reading to every registered panel, mirroring what
/// <see cref="Hubs.EnvironmentReadingsHub" /> pushes to web clients - this is what keeps the panel's current
/// reading live between (far less frequent) ControlStateChanged pushes.
/// </summary>
public class BroadcastHmiEnvironmentReadingHandler(KelvinContext context, IHmiOutboundChannel outboundChannel)
  : IHandler<BroadcastHmiEnvironmentReadingRequest>
{
  public async Task<Result> HandleAsync(BroadcastHmiEnvironmentReadingRequest request, CancellationToken ct = default)
  {
    var macAddresses = await context
      .Hmis.Where(hmi => hmi.Enabled && hmi.DeletedAt == null && hmi.MacAddress != null)
      .Select(hmi => hmi.MacAddress!)
      .ToListAsync(ct);

    if (macAddresses.Count == 0)
      return Result.Success();

    var payload = HmiEnvironmentReadingEncoder.Encode(request.Reading);
    foreach (var macAddress in macAddresses)
    {
      outboundChannel.Write(macAddress, payload);
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
