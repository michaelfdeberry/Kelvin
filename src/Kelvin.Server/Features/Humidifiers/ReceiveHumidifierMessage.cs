using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Features.Devices;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Humidifiers;

/// <summary>A humidifier frame (tag already stripped); the response is the reply frame content to send back.</summary>
public record ReceiveHumidifierMessageRequest(string MacAddress, byte[] Payload) : IRequest<byte[]>;

public static class ReceiveHumidifierMessageErrors
{
  public static readonly Error InvalidPayload = new("ReceiveHumidifierMessage.InvalidPayload", "The humidifier message payload was invalid.");
}

/// <summary>
/// Answers a humidifier's periodic ping with whether its relay should be on, registering unknown devices by MAC.
/// </summary>
public class ReceiveHumidifierMessageHandler(
  KelvinContext context,
  TimeProvider time,
  IHandler<EvaluateHumidifierRequest, EvaluateHumidifierResponse> evaluateHumidifier,
  IHandler<UpdateDeviceStateRequest> deviceStates,
  ILogger<ReceiveHumidifierMessageHandler> logger
) : IHandler<ReceiveHumidifierMessageRequest, byte[]>
{
  public async Task<Result<byte[]>> HandleAsync(ReceiveHumidifierMessageRequest request, CancellationToken ct = default)
  {
    if (!HumidifierProtocol.TryDecodePing(request.Payload, out var relayOn))
      return Result<byte[]>.Failure(ReceiveHumidifierMessageErrors.InvalidPayload);

    Result<EvaluateHumidifierResponse> evaluation;
    HumidifierDevice? device = null;
    try
    {
      device = await RegisterAsync(request.MacAddress, ct);
      evaluation = await evaluateHumidifier.HandleAsync(new EvaluateHumidifierRequest(device.Id, relayOn), ct);
    }
    catch (Exception exception) when (!ct.IsCancellationRequested)
    {
      logger.LogError(exception, "Humidifier {MacAddress} evaluation failed, turning it off.", request.MacAddress);
      await PublishStateAsync(device, false, ct);
      return Result<byte[]>.Success(HumidifierProtocol.EncodeState(false));
    }

    var enabled = evaluation.IsSuccess && evaluation.Value!.Enabled;
    await PublishStateAsync(device, enabled, ct);

    if (evaluation.IsFailure)
    {
      logger.LogWarning("Humidifier {MacAddress} evaluation failed, turning it off: {Error}", request.MacAddress, evaluation.Error);
    }
    else if (enabled != relayOn)
    {
      logger.LogInformation(
        "Humidifier {MacAddress} turning {State}: {Reason} (humidity {Humidity}%, target {Target}%, outdoor {Outdoor}C)",
        request.MacAddress,
        enabled ? "on" : "off",
        evaluation.Value!.Reason,
        evaluation.Value.HumidityPercentage,
        evaluation.Value.TargetHumidityPercentage,
        evaluation.Value.OutdoorTemperatureC
      );
    }

    return Result<byte[]>.Success(HumidifierProtocol.EncodeState(enabled));
  }

  private Task PublishStateAsync(HumidifierDevice? device, bool enabled, CancellationToken ct)
  {
    if (device is null)
      return Task.CompletedTask;

    return deviceStates.HandleAsync(new UpdateDeviceStateRequest(DeviceStateNames.Humidifier, device.Enabled, new HumidifierState(enabled)), ct);
  }

  private async Task<HumidifierDevice> RegisterAsync(string macAddress, CancellationToken ct)
  {
    var device = await context.Devices.OfType<HumidifierDevice>().FirstOrDefaultAsync(d => d.MacAddress == macAddress, ct);
    if (device is null)
    {
      device = new HumidifierDevice
      {
        Name = "Humidifier",
        MacAddress = macAddress,
        Enabled = true,
        Configuration = new HumidifierConfiguration(),
      };
      context.Devices.Add(device);
    }
    // Mirrors the Hmi/Sensor behavior: a deleted device that reappears is restored, but left disabled.
    else if (device.DeletedAt is not null)
    {
      device.Enabled = false;
      device.DeletedAt = null;
    }

    device.LastSeenAt = time.GetUtcNow();
    await context.SaveChangesAsync(ct);
    return device;
  }
}

public class ReceiveHumidifierMessageRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<ReceiveHumidifierMessageRequest, byte[]>, ReceiveHumidifierMessageHandler>();
  }
}
