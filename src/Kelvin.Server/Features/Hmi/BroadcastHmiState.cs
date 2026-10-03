using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Hmi;

public record BroadcastHmiStateRequest() : IRequest;

/// <summary>
/// Pushes the full thermostat state to every registered panel, split into ESP-NOW-sized chunks since the
/// schedule list is open-ended and can exceed a single ~250 byte frame.
/// </summary>
public class BroadcastHmiStateHandler(KelvinContext context, IHmiOutboundChannel outboundChannel) : IHandler<BroadcastHmiStateRequest>
{
  public async Task<Result> HandleAsync(BroadcastHmiStateRequest request, CancellationToken ct = default)
  {
    var macAddresses = await context
      .Hmis.Where(hmi => hmi.Enabled && hmi.DeletedAt == null && hmi.MacAddress != null)
      .Select(hmi => hmi.MacAddress!)
      .ToListAsync(ct);

    if (macAddresses.Count == 0)
      return Result.Success();

    var thermostat = await context.Thermostats.Include(t => t.SetPoints).Include(t => t.Schedules).FirstOrDefaultAsync(ct);
    if (thermostat is null)
      return Result.Success();

    var frames = HmiEnvelope.Encode(HmiMessageType.ThermostatStateChunk, HmiThermostatStateEncoder.Encode(thermostat));
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

public class BroadcastHmiStateFeature : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<BroadcastHmiStateRequest>, BroadcastHmiStateHandler>();
  }
}
