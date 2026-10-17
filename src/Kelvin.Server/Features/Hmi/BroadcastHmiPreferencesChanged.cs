using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Hmi;

public record BroadcastHmiPreferencesChangedRequest() : IRequest;

public static class BroadcastHmiPreferencesChangedErrors
{
  public static readonly Error DefaultError = new("BroadcastHmiPreferencesChanged.Failed", "An error occurred processing the request.");
}

public class BroadcastHmiPreferencesChangedHandler(KelvinContext context, IHmiOutboundChannel outboundChannel)
  : IHandler<BroadcastHmiPreferencesChangedRequest>
{
  public async Task<Result> HandleAsync(BroadcastHmiPreferencesChangedRequest request, CancellationToken ct = default)
  {
    var macAddresses = await context
      .Hmis.Where(hmi => hmi.Enabled && hmi.DeletedAt == null && hmi.MacAddress != null)
      .Select(hmi => hmi.MacAddress!)
      .ToListAsync(ct);

    if (macAddresses.Count == 0)
      return Result.Success();

    var preferences = await context.Preferences.FirstOrDefaultAsync(ct);
    if (preferences is null)
      return Result.Success();

    var frames = HmiEnvelope.Encode(HmiMessageType.PreferencesChanged, HmiPreferencesEncoder.Encode(preferences));
    foreach (var macAddress in macAddresses)
    {
      foreach (var frame in frames)
      {
        outboundChannel.Write(macAddress, frame);
      }
    }

    return Result.Success();
  }
}

public class BroadcastHmiPreferencesChangedRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<BroadcastHmiPreferencesChangedRequest>, BroadcastHmiPreferencesChangedHandler>();
  }
}
