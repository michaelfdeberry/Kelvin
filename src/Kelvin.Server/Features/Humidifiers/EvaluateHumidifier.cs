using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Features.Control;
using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Features.Weather;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Humidifiers;

/// <param name="RelayOn">What the device reports its relay is doing now, used to apply hysteresis.</param>
public record EvaluateHumidifierRequest(Guid DeviceId, bool RelayOn) : IRequest<EvaluateHumidifierResponse>;

public record EvaluateHumidifierResponse(
  bool Enabled,
  string Reason,
  float? HumidityPercentage = null,
  float? TargetHumidityPercentage = null,
  float? OutdoorTemperatureC = null
);

public static class EvaluateHumidifierErrors
{
  public static readonly Error DeviceNotFound = new("EvaluateHumidifier.DeviceNotFound", "The humidifier was not found.");
}

/// <summary>
/// Decides whether a humidifier should run: it must be enabled, in season, the furnace must be heating under
/// Kelvin's control, the outdoor temperature must be within the configured range, and the indoor humidity must be
/// below its target. Anything that can't be determined fails safe to off.
/// </summary>
public class EvaluateHumidifierHandler(
  KelvinContext context,
  TimeProvider time,
  IHandler<GetControlStateRequest, GetControlStateResponse> getControlState,
  IHandler<GetWeatherForecastRequest, GetWeatherForecastResponse> getWeatherForecast,
  IHandler<GetLatestReadingsRequest, GetLatestReadingsResponse> getLatestReadings
) : IHandler<EvaluateHumidifierRequest, EvaluateHumidifierResponse>
{
  /// <summary>Once running, keep running until the target is reached; once off, wait until this far below it.</summary>
  public const float HysteresisPercentage = 2f;

  /// <summary>A single sensor at the target this far above the average stops the humidifier.</summary>
  public const float SensorMarginPercentage = 5f;

  public async Task<Result<EvaluateHumidifierResponse>> HandleAsync(EvaluateHumidifierRequest request, CancellationToken ct = default)
  {
    var device = await context
      .Devices.AsNoTracking()
      .OfType<HumidifierDevice>()
      .FirstOrDefaultAsync(d => d.Id == request.DeviceId && d.DeletedAt == null, ct);
    if (device is null)
      return Result<EvaluateHumidifierResponse>.Failure(EvaluateHumidifierErrors.DeviceNotFound);

    if (!device.Enabled)
      return Off("The humidifier is disabled.");

    var configuration = device.Configuration;
    if (!configuration.IsInSeason(time.GetLocalNow().Month))
      return Off("Outside of the humidification season.");

    var control = await getControlState.HandleAsync(new GetControlStateRequest(), ct);
    if (control.IsFailure || control.Value!.ControlState != ControlState.Enable || control.Value.CallState != ControlState.Heating)
      return Off("The system is not heating.");

    // Evaluated on a short timer, so never wait on the weather service; ThermostatService keeps the cache warm.
    var forecast = await getWeatherForecast.HandleAsync(new GetWeatherForecastRequest(CachedOnly: true), ct);
    var outdoorTemperatureC = forecast.IsSuccess ? forecast.Value!.Current?.TemperatureC : null;
    if (outdoorTemperatureC is null)
      return Off("The outdoor temperature is unavailable.");

    var targetHumidity = configuration.TargetHumidityFor(outdoorTemperatureC.Value);
    if (targetHumidity is null)
      return Off("The outdoor temperature is above the humidification range.", outdoorTemperatureC: outdoorTemperatureC);

    var readings = await getLatestReadings.HandleAsync(new GetLatestReadingsRequest(), ct);
    var humidity = readings.IsSuccess ? readings.Value!.Reading.HumidityPercentage : 0;
    // No humidity sensors averages to 0, which is never a real indoor reading.
    if (humidity <= 0)
      return Off("The indoor humidity is unavailable.", targetHumidity, outdoorTemperatureC);

    var threshold = request.RelayOn ? targetHumidity.Value : targetHumidity.Value - HysteresisPercentage;
    var enabled = humidity < threshold;

    // Guards against the average hiding one damp area (or a few sensors reading low).
    var highestSensor = readings.Value!.Reading.Areas.Values.Select(packet => packet.HumidityPercentage).DefaultIfEmpty(0).Max();
    if (enabled && highestSensor >= targetHumidity.Value && highestSensor >= humidity + SensorMarginPercentage)
    {
      return Result<EvaluateHumidifierResponse>.Success(
        new EvaluateHumidifierResponse(false, "A sensor has reached the target humidity.", humidity, targetHumidity, outdoorTemperatureC)
      );
    }

    return Result<EvaluateHumidifierResponse>.Success(
      new EvaluateHumidifierResponse(
        enabled,
        enabled ? "The indoor humidity is below the target." : "The indoor humidity is at or near the target.",
        humidity,
        targetHumidity,
        outdoorTemperatureC
      )
    );
  }

  private static Result<EvaluateHumidifierResponse> Off(string reason, float? targetHumidity = null, float? outdoorTemperatureC = null) =>
    Result<EvaluateHumidifierResponse>.Success(new EvaluateHumidifierResponse(false, reason, null, targetHumidity, outdoorTemperatureC));
}

public class EvaluateHumidifierRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<EvaluateHumidifierRequest, EvaluateHumidifierResponse>, EvaluateHumidifierHandler>();
  }
}
