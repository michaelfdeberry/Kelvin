using Kelvin.Server.Application;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Models;

namespace Kelvin.Server.Services;

public enum FanOwner
{
  Recirculation,
  Balancing,
}

/// <summary>
/// Coordinates the services that run the fan on their own behalf, so one finishing never switches the fan off
/// underneath another, and the fan returns to the state it was in before any of them took it.
/// </summary>
/// <remarks>
/// Callers pass the thermostat they already read this iteration rather than the arbiter fetching it again.
/// </remarks>
public interface IFanArbiter
{
  Task<Result> AcquireAsync(FanOwner owner, Thermostat thermostat, CancellationToken cancellationToken);

  Task<Result> ReleaseAsync(FanOwner owner, Thermostat thermostat, CancellationToken cancellationToken);
}

public class FanArbiter(IDispatcher dispatcher) : IFanArbiter
{
  private readonly SemaphoreSlim _gate = new(1, 1);
  private readonly HashSet<FanOwner> _owners = [];
  private bool _fanEnabledBeforeOwnership;

  public async Task<Result> AcquireAsync(FanOwner owner, Thermostat thermostat, CancellationToken cancellationToken)
  {
    await _gate.WaitAsync(cancellationToken);
    try
    {
      if (_owners.Contains(owner))
        return Result.Success();

      if (_owners.Count == 0)
      {
        var fanWasEnabled = thermostat.FanEnabled;
        if (!fanWasEnabled)
        {
          var update = await dispatcher.DispatchAsync(new UpdateThermostatRequest(thermostat.Mode, FanEnabled: true), cancellationToken);
          if (update.IsFailure)
            return update;
        }

        _fanEnabledBeforeOwnership = fanWasEnabled;
      }

      _owners.Add(owner);
      return Result.Success();
    }
    finally
    {
      _gate.Release();
    }
  }

  public async Task<Result> ReleaseAsync(FanOwner owner, Thermostat thermostat, CancellationToken cancellationToken)
  {
    await _gate.WaitAsync(cancellationToken);
    try
    {
      if (!_owners.Contains(owner))
        return Result.Success();

      if (_owners.Count == 1 && thermostat.FanEnabled != _fanEnabledBeforeOwnership)
      {
        var update = await dispatcher.DispatchAsync(new UpdateThermostatRequest(thermostat.Mode, _fanEnabledBeforeOwnership), cancellationToken);
        if (update.IsFailure)
          return update;
      }

      // Removed only once the fan is restored, so a failed release is retried by the caller.
      _owners.Remove(owner);
      return Result.Success();
    }
    finally
    {
      _gate.Release();
    }
  }
}
