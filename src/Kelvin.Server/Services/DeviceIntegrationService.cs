using Kelvin.Server.Application;
using Kelvin.Server.Features.Humidifiers;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;

namespace Kelvin.Server.Services;

/// <summary>
/// The integration point for devices that follow the HVAC call (currently the humidifier). Re-evaluates them
/// whenever the applied call changes or a device changes, and on a short interval so running devices keep
/// receiving state and humidity changes are picked up between calls.
/// </summary>
public class DeviceIntegrationService(ILogger<DeviceIntegrationService> logger, TimeProvider time, IDispatcher dispatcher)
  : BackgroundService,
    IEventHandler<ControlStateChangedEvent>,
    IEventHandler<DevicesChangedEvent>
{
  /// <summary>Well inside the humidifier's 2 minute fail-safe, so a few lost frames don't release the relay.</summary>
  public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

  private volatile TaskCompletionSource _changed = NewSignal();

  private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

  /// <summary>Called from a scoped publish, so it only wakes the loop.</summary>
  public Task HandleAsync(ControlStateChangedEvent @event, CancellationToken cancellationToken = default)
  {
    if (@event.Change.Kind == ControlChangeKind.Call)
      _changed.TrySetResult();

    return Task.CompletedTask;
  }

  /// <summary>Called from a scoped publish, so it only wakes the loop.</summary>
  public Task HandleAsync(DevicesChangedEvent @event, CancellationToken cancellationToken = default)
  {
    _changed.TrySetResult();
    return Task.CompletedTask;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    // The devices' last known state is unknown after a restart, so the first pass always sends.
    var force = true;
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await dispatcher.DispatchAsync(new SyncHumidifierRequest(force), stoppingToken);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        break;
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "An error occurred while syncing the humidifier.");
      }

      force = await WaitForChangeAsync(stoppingToken);
    }
  }

  /// <summary>Returns true when woken by a change, false when the refresh interval elapsed.</summary>
  private async Task<bool> WaitForChangeAsync(CancellationToken stoppingToken)
  {
    using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
    var changed = _changed.Task;
    var delay = Task.Delay(RefreshInterval, time, delayCancellation.Token);

    try
    {
      if (await Task.WhenAny(changed, delay) != changed)
      {
        await delay;
        return false;
      }
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
      return false;
    }

    delayCancellation.Cancel();
    _changed = NewSignal();
    return true;
  }
}
