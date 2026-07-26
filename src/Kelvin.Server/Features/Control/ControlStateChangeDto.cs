using Kelvin.Server.Models;

namespace Kelvin.Server.Features.Control;

/// <summary>
/// The shape a recorded control state change is exposed in, shared by the history, current state and live
/// broadcast surfaces so every consumer sees the same contract.
/// </summary>
public record ControlStateChangeDto(
  Guid Id,
  ControlChangeKind Kind,
  ControlState State,
  ControlState? PreviousState,
  DateTimeOffset ChangedAt,
  double? PreviousStateDurationSeconds,
  string? Reason,
  float? EnvironmentTemperatureC,
  float? HumidityPercentage,
  float? TargetTemperatureC,
  float? HysteresisC,
  float? ForecastTemperatureC,
  RunMode? Mode,
  Guid? ScheduleId,
  Guid? SetPointId
)
{
  public static ControlStateChangeDto FromEntity(ControlStateChange change) =>
    new(
      change.Id,
      change.Kind,
      change.State,
      change.PreviousState,
      change.CreatedAt,
      change.PreviousStateDurationSeconds,
      change.Reason,
      change.EnvironmentTemperatureC,
      change.HumidityPercentage,
      change.TargetTemperatureC,
      change.HysteresisC,
      change.ForecastTemperatureC,
      change.Mode,
      change.ScheduleId,
      change.SetPointId
    );
}
