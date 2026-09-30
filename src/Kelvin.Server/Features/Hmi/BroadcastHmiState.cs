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
  // ESP-NOW's payload limit is ~250 bytes; leave headroom for the 3 byte chunk header plus the gateway's
  // MAC + length framing on the serial link.
  private const int MAX_CHUNK_DATA_SIZE = 200;

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

    var chunks = ChunkPayload(HmiThermostatStateEncoder.Encode(thermostat));
    foreach (var macAddress in macAddresses)
    {
      foreach (var chunk in chunks)
      {
        outboundChannel.Write(macAddress, chunk);
      }
    }

    return Result.Success();
  }

  private static List<byte[]> ChunkPayload(byte[] data)
  {
    var chunkCount = (byte)Math.Max(1, (int)Math.Ceiling(data.Length / (double)MAX_CHUNK_DATA_SIZE));
    var chunks = new List<byte[]>(chunkCount);

    for (byte chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
    {
      var start = chunkIndex * MAX_CHUNK_DATA_SIZE;
      var length = Math.Min(MAX_CHUNK_DATA_SIZE, data.Length - start);
      var chunk = new byte[3 + length];
      chunk[0] = (byte)HmiMessageType.ThermostatStateChunk;
      chunk[1] = chunkIndex;
      chunk[2] = chunkCount;
      Buffer.BlockCopy(data, start, chunk, 3, length);
      chunks.Add(chunk);
    }

    return chunks;
  }
}

public class BroadcastHmiStateFeature : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<BroadcastHmiStateRequest>, BroadcastHmiStateHandler>();
  }
}
