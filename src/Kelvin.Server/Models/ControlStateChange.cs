namespace Kelvin.Server.Models;

/// <summary>
/// Which of the control service's independent state axes a <see cref="ControlStateChange" /> belongs to.
/// </summary>
/// <remarks>
/// The axes move independently of each other, so each forms its own timeline. Durations are only meaningful
/// within an axis: the fan can be switched on and off without the heating call changing, and vice versa.
/// </remarks>
public enum ControlChangeKind
{
  /// <summary>Ownership of the equipment, driven only by <see cref="ControlState.Enable" />/<see cref="ControlState.Disable" />.</summary>
  Control,

  /// <summary>The active HVAC call: <see cref="ControlState.Dwell" />, <see cref="ControlState.Heating" /> or <see cref="ControlState.Cooling" />.</summary>
  Call,

  /// <summary>The fan, which is actuated independently of the current call.</summary>
  Fan,
}

/// <summary>
/// A record of the control service actuating a relay, forming the history the statistics and live status are
/// derived from.
/// </summary>
/// <remarks>
/// Only changes that actually moved a relay are recorded. Requests that were blocked by the minimum on/off
/// duration guards, ignored while control was reverted, or coalesced into an already pending transition are
/// logged but produce no row - the history is a record of what the equipment did, not of what was asked for.
/// <para>
/// There is no dedicated timestamp: <see cref="Entity.CreatedAt" /> is the moment of the change, stamped from the
/// shared <see cref="TimeProvider" /> when the row is saved. <see cref="PreviousStateDurationSeconds" /> is
/// measured by the control service itself rather than derived from adjacent rows, so how long the equipment ran
/// is accurate even if persistence lagged behind the actuation.
/// </para>
/// </remarks>
public class ControlStateChange : Entity
{
  /// <summary>Which state axis moved.</summary>
  public ControlChangeKind Kind { get; set; }

  /// <summary>The state the axis moved to.</summary>
  public ControlState State { get; set; }

  /// <summary>The state the axis moved from, null when this is the first change recorded for the axis.</summary>
  public ControlState? PreviousState { get; set; }

  /// <summary>
  /// How long <see cref="PreviousState" /> was held, null when there was no previous state.
  /// </summary>
  public double? PreviousStateDurationSeconds { get; set; }

  /// <summary>Why the change happened, when the producer supplied an explanation.</summary>
  public string? Reason { get; set; }

  /// <summary>The average indoor temperature at the time of the change, in degrees Celsius.</summary>
  public float? EnvironmentTemperatureC { get; set; }

  /// <summary>The average indoor humidity at the time of the change, as a percentage.</summary>
  public float? HumidityPercentage { get; set; }

  /// <summary>The average indoor CO2 level at the time of the change, in parts per million (ppm).</summary>
  public float? CO2LevelPpm { get; set; }

  /// <summary>The temperature the system was working towards, in degrees Celsius.</summary>
  public float? TargetTemperatureC { get; set; }

  /// <summary>The hysteresis that was in effect, in degrees Celsius.</summary>
  public float? HysteresisC { get; set; }

  /// <summary>The forecast outdoor temperature that was considered, in degrees Celsius.</summary>
  public float? ForecastTemperatureC { get; set; }

  /// <summary>The thermostat run mode at the time of the change.</summary>
  public RunMode? Mode { get; set; }

  /// <summary>The schedule that drove the change, when one was active.</summary>
  public Guid? ScheduleId { get; set; }

  /// <summary>The set point that drove the change, when no schedule was active.</summary>
  public Guid? SetPointId { get; set; }
}
