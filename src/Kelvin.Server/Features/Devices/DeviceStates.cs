using System.Text.Json.Serialization;
using Kelvin.Server.Application;
using Kelvin.Server.Messaging;

namespace Kelvin.Server.Features.Devices;

/// <summary>The keys states are stored and broadcast under; each device type adds its own.</summary>
public static class DeviceStateNames
{
  public const string Humidifier = "humidifier";
}

/// <summary>
/// The state a kind of device reports. A device that stops reporting for longer than <see cref="Timeout" /> is
/// assumed to have fallen back to <see cref="Default" />, so a stale state is never shown as current.
/// </summary>
public abstract record DeviceState
{
  /// <summary>The state to assume once reports stop.</summary>
  [JsonIgnore]
  public abstract DeviceState Default { get; }

  /// <summary>How long a state stays current without being reported again.</summary>
  [JsonIgnore]
  public abstract TimeSpan Timeout { get; }
}

/// <param name="Enabled">Whether the humidifier is currently running.</param>
public record HumidifierState(bool Enabled) : DeviceState
{
  /// <summary>Just past the humidifier's two minute fail-safe, after which it releases its relay without any pushes.</summary>
  public static readonly TimeSpan ReportTimeout = TimeSpan.FromSeconds(150);

  [JsonIgnore]
  public override DeviceState Default => new HumidifierState(false);

  [JsonIgnore]
  public override TimeSpan Timeout => ReportTimeout;
}

/// <param name="DeviceEnabled">Disabled devices have their state dropped, and their state is never broadcast.</param>
/// <param name="Commanded">
/// The state was sent to the device rather than reported by it. It never keeps a state alive, and is ignored once the
/// state has timed out until the device reports again, so an unreachable device isn't shown as running.
/// </param>
public record UpdateDeviceStateRequest(string Name, bool DeviceEnabled, DeviceState State, bool Commanded = false) : IRequest;

public record GetDeviceStatesRequest() : IRequest<GetDeviceStatesResponse>;

public record GetDeviceStatesResponse(Dictionary<string, object> States);

/// <summary>Reverts the states that haven't been reported within their timeout to their default, broadcasting any change.</summary>
public record ExpireDeviceStatesRequest() : IRequest;

/// <summary>
/// Holds the latest state of each kind of device, by name, and broadcasts the full set whenever one changes.
/// Registered as a singleton so the cache outlives individual requests.
/// </summary>
public class DeviceStateHandler(IDispatcher dispatcher, TimeProvider time)
  : IHandler<UpdateDeviceStateRequest>,
    IHandler<GetDeviceStatesRequest, GetDeviceStatesResponse>,
    IHandler<ExpireDeviceStatesRequest>
{
  private sealed record Entry(DeviceState State, DateTimeOffset UpdatedAt, bool TimedOut = false)
  {
    public bool IsExpired(DateTimeOffset now) => !TimedOut && now - UpdatedAt > State.Timeout;
  }

  private readonly object _lock = new();
  private readonly Dictionary<string, Entry> _states = [];

  public async Task<Result> HandleAsync(UpdateDeviceStateRequest request, CancellationToken ct = default)
  {
    Dictionary<string, object> snapshot;
    lock (_lock)
    {
      var now = time.GetUtcNow();
      var known = _states.TryGetValue(request.Name, out var existing);

      if (!request.DeviceEnabled)
      {
        // Only broadcast when a state was actually dropped, so clients don't keep showing it.
        if (!_states.Remove(request.Name))
          return Result.Success();
      }
      else if (request.Commanded && known && existing!.TimedOut)
      {
        return Result.Success();
      }
      else if (known && existing!.State.Equals(request.State))
      {
        // Devices report every few seconds, so only a real change is worth broadcasting. A report keeps the
        // state alive, but a command doesn't, as it says nothing about whether the device is still there.
        if (!request.Commanded)
          _states[request.Name] = new Entry(existing.State, now);

        return Result.Success();
      }
      else
      {
        _states[request.Name] = new Entry(request.State, now);
      }

      snapshot = Snapshot(now);
    }

    await dispatcher.PublishAsync(new DeviceStateChangedEvent(snapshot), ct);
    return Result.Success();
  }

  public async Task<Result> HandleAsync(ExpireDeviceStatesRequest request, CancellationToken ct = default)
  {
    Dictionary<string, object> snapshot;
    lock (_lock)
    {
      var now = time.GetUtcNow();
      var expired = _states.Where(s => s.Value.IsExpired(now) && !s.Value.State.Equals(s.Value.State.Default)).Select(s => s.Key).ToList();
      if (expired.Count == 0)
        return Result.Success();

      foreach (var name in expired)
      {
        _states[name] = new Entry(_states[name].State.Default, now, TimedOut: true);
      }

      snapshot = Snapshot(now);
    }

    await dispatcher.PublishAsync(new DeviceStateChangedEvent(snapshot), ct);
    return Result.Success();
  }

  public Task<Result<GetDeviceStatesResponse>> HandleAsync(GetDeviceStatesRequest request, CancellationToken ct = default)
  {
    lock (_lock)
    {
      return Task.FromResult(Result<GetDeviceStatesResponse>.Success(new GetDeviceStatesResponse(Snapshot(time.GetUtcNow()))));
    }
  }

  /// <summary>Reads as the default for a state that has timed out but isn't yet reverted by an expiry pass.</summary>
  private Dictionary<string, object> Snapshot(DateTimeOffset now) =>
    _states.ToDictionary(s => s.Key, s => s.Value.IsExpired(now) ? s.Value.State.Default : (object)s.Value.State);
}

public class DeviceStateRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddSingleton<DeviceStateHandler>();
    services.AddSingleton<IHandler<UpdateDeviceStateRequest>>(sp => sp.GetRequiredService<DeviceStateHandler>());
    services.AddSingleton<IHandler<GetDeviceStatesRequest, GetDeviceStatesResponse>>(sp => sp.GetRequiredService<DeviceStateHandler>());
    services.AddSingleton<IHandler<ExpireDeviceStatesRequest>>(sp => sp.GetRequiredService<DeviceStateHandler>());
  }
}
