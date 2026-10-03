using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Hmi;

public record BroadcastHmiControlStateRequest(ControlStateChange Change) : IRequest;

/// <summary>
/// Pushes a live Call-kind state change to every registered panel, so the HEATING/COOLING badge tracks the
/// actual HVAC call rather than just the configured <see cref="Models.Thermostat" /> mode.
/// </summary>
public class BroadcastHmiControlStateHandler(KelvinContext context, IHmiOutboundChannel outboundChannel) : IHandler<BroadcastHmiControlStateRequest>
{
  public async Task<Result> HandleAsync(BroadcastHmiControlStateRequest request, CancellationToken ct = default)
  {
    if (request.Change.Kind != ControlChangeKind.Call)
      return Result.Success();

    var macAddresses = await context
      .Hmis.Where(hmi => hmi.Enabled && hmi.DeletedAt == null && hmi.MacAddress != null)
      .Select(hmi => hmi.MacAddress!)
      .ToListAsync(ct);

    if (macAddresses.Count == 0)
      return Result.Success();

    var payload = HmiControlStateEncoder.Encode(request.Change);
    foreach (var macAddress in macAddresses)
    {
      outboundChannel.Write(macAddress, payload);
    }

    return Result.Success();
  }
}

public class BroadcastHmiControlStateFeature : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<BroadcastHmiControlStateRequest>, BroadcastHmiControlStateHandler>();
  }
}
