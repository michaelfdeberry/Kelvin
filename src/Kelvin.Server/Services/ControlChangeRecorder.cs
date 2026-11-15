using Kelvin.Server.Application;
using Kelvin.Server.Features.Control;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;

namespace Kelvin.Server.Services;

internal sealed class ControlChangeRecorder(
  ILogger<ControlService> logger,
  IDispatcher dispatcher,
  TimeProvider time
)
{
  private readonly List<ControlStateChange> _pendingChanges = [];

  /// <summary>Queues a record after the relay has moved successfully.</summary>
  public void RecordChange(
    ControlChangeKind kind,
    ControlState state,
    ControlState? previousState,
    DateTimeOffset? previousSince,
    string? reason,
    ControlMessage? context
  )
  {
    // Measure duration now rather than deriving it from the stored timeline after a potentially delayed save.
    var change = new ControlStateChange
    {
      Kind = kind,
      State = state,
      PreviousState = previousState,
      PreviousStateDurationSeconds = previousSince is null ? null : (time.GetUtcNow() - previousSince.Value).TotalSeconds,
      Reason = reason ?? context?.Reason,
      EnvironmentTemperatureC = context?.EnvironmentTemperatureC,
      HumidityPercentage = context?.HumidityPercentage,
      TargetTemperatureC = context?.TargetTemperatureC,
      CO2LevelPpm = context?.CO2LevelPpm,
      HysteresisC = context?.HysteresisC,
      ForecastTemperatureC = context?.ForecastTemperatureC,
      Mode = context?.Mode,
      ScheduleId = context?.ScheduleId,
      SetPointId = context?.SetPointId,
    };
    _pendingChanges.Add(change);
  }

  /// <summary>Broadcasts and persists queued changes without letting reporting failures stop control.</summary>
  public async Task FlushChangesAsync(CancellationToken cancellationToken)
  {
    if (_pendingChanges.Count == 0)
      return;

    try
    {
      foreach (var change in _pendingChanges)
      {
        var result = await dispatcher.DispatchAsync(new SaveControlStateChangeRequest(change), cancellationToken);

        if (result.IsFailure)
          logger.LogError("Failed to record the {Kind} state change to {State}: {Error}", change.Kind, change.State, result.Error.Message);

        await dispatcher.PublishAsync(new ControlStateChangedEvent(change), cancellationToken);
      }
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "An error occurred while recording control state changes.");
    }
    finally
    {
      _pendingChanges.Clear();
    }
  }

  public async Task RecordStartupEventAsync(ControlMessage? context, CancellationToken cancellationToken)
  {
    try
    {
      var previous = await GetLatestLifecycleStateAsync(cancellationToken);
      ControlState? previousState = previous?.State == ControlState.Fault ? ControlState.Fault : null;
      var previousSince = previousState is null ? null : previous?.ChangedAt;

      RecordChange(ControlChangeKind.Lifecycle, ControlState.Startup, previousState, previousSince, "control service started", context);
      await FlushChangesAsync(cancellationToken);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "An error occurred while recording the control service startup event.");
    }
  }

  public async Task RecordFaultEventAsync(string reason, ControlMessage? context, CancellationToken cancellationToken)
  {
    try
    {
      var previous = await GetLatestLifecycleStateAsync(cancellationToken);
      ControlState? previousState = previous?.State is ControlState.Startup or ControlState.Fault ? previous.State : null;
      var previousSince = previousState is null ? null : previous?.ChangedAt;

      RecordChange(ControlChangeKind.Lifecycle, ControlState.Fault, previousState, previousSince, reason, context);
      await FlushChangesAsync(cancellationToken);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "An error occurred while recording the control service fault event.");
    }
  }

  private async Task<GetLatestControlStateChangeResponse?> GetLatestLifecycleStateAsync(CancellationToken cancellationToken)
  {
    var result = await dispatcher.DispatchAsync<GetLatestControlStateChangeRequest, GetLatestControlStateChangeResponse?>(
      new(ControlChangeKind.Lifecycle),
      cancellationToken
    );

    result.EnsureSuccess();
    return result.Value;
  }
}
