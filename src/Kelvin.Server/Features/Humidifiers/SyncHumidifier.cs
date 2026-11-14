using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Data;
using Kelvin.Server.Features.Devices;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Humidifiers;

/// <param name="Force">Send the state even when nothing suggests the device needs it, e.g. after a change or check-in.</param>
public record SyncHumidifierRequest(bool Force) : IRequest;

/// <summary>
/// Evaluates the humidifier and queues its state. While it should run the state is resent every time, since the
/// device releases its relay when the pushes stop; otherwise it's only sent when forced or the device last
/// acknowledged running.
/// </summary>
public class SyncHumidifierHandler(
  KelvinContext context,
  IHandler<EvaluateHumidifierRequest, EvaluateHumidifierResponse> evaluateHumidifier,
  IHandler<GetDeviceStatesRequest, GetDeviceStatesResponse> getDeviceStates,
  IDeviceOutboundChannel outboundChannel,
  ILogger<SyncHumidifierHandler> logger
) : IHandler<SyncHumidifierRequest>
{
  public async Task<Result> HandleAsync(SyncHumidifierRequest request, CancellationToken ct = default)
  {
    var device = await context
      .Devices.AsNoTracking()
      .OfType<HumidifierDevice>()
      .FirstOrDefaultAsync(d => d.DeletedAt == null && d.MacAddress != null, ct);
    if (device is null)
      return Result.Success();

    var states = await getDeviceStates.HandleAsync(new GetDeviceStatesRequest(), ct);
    bool? relayOn =
      states.IsSuccess && states.Value!.States.TryGetValue(DeviceStateNames.Humidifier, out var state) && state is HumidifierState reported
        ? reported.Enabled
        : null;

    bool enabled;
    try
    {
      var evaluation = await evaluateHumidifier.HandleAsync(new EvaluateHumidifierRequest(device.Id, relayOn ?? false), ct);
      enabled = evaluation.IsSuccess && evaluation.Value!.Enabled;

      if (evaluation.IsFailure)
      {
        logger.LogWarning("Humidifier {MacAddress} evaluation failed, turning it off: {Error}", device.MacAddress, evaluation.Error);
      }
      else if (relayOn is not null && enabled != relayOn)
      {
        logger.LogInformation(
          "Humidifier {MacAddress} turning {State}: {Reason} (humidity {Humidity}%, target {Target}%, outdoor {Outdoor}C)",
          device.MacAddress,
          enabled ? "on" : "off",
          evaluation.Value!.Reason,
          evaluation.Value.HumidityPercentage,
          evaluation.Value.TargetHumidityPercentage,
          evaluation.Value.OutdoorTemperatureC
        );
      }
    }
    catch (Exception exception) when (!ct.IsCancellationRequested)
    {
      logger.LogError(exception, "Humidifier {MacAddress} evaluation failed, turning it off.", device.MacAddress);
      enabled = false;
    }

    if (request.Force || enabled || relayOn == true)
    {
      outboundChannel.Write(device.MacAddress!, FrameTags.Humidifier, (byte)HumidifierMessageType.State, HumidifierProtocol.EncodeState(enabled));
    }

    return Result.Success();
  }
}

public class SyncHumidifierRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<SyncHumidifierRequest>, SyncHumidifierHandler>();
  }
}
