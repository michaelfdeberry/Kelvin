using Kelvin.Server.Application;
using Kelvin.Server.Messaging;

namespace Kelvin.Server.Features.Devices;

/// <summary>The keys states are stored and broadcast under; each device type adds its own.</summary>
public static class DeviceStateNames
{
  public const string Humidifier = "humidifier";
}

/// <param name="Enabled">Whether the humidifier is currently running.</param>
public record HumidifierState(bool Enabled);

/// <param name="DeviceEnabled">Disabled devices have their state dropped and are never broadcast.</param>
public record UpdateDeviceStateRequest(string Name, bool DeviceEnabled, object State) : IRequest;

public record GetDeviceStatesRequest() : IRequest<GetDeviceStatesResponse>;

public record GetDeviceStatesResponse(Dictionary<string, object> States);

/// <summary>
/// Holds the latest reported state of each kind of device, by name, and broadcasts the full set whenever one changes.
/// Registered as a singleton so the cache outlives individual requests.
/// </summary>
public class DeviceStateHandler(IDispatcher dispatcher)
  : IHandler<UpdateDeviceStateRequest>,
    IHandler<GetDeviceStatesRequest, GetDeviceStatesResponse>
{
  private readonly object _lock = new();
  private readonly Dictionary<string, object> _states = [];

  public async Task<Result> HandleAsync(UpdateDeviceStateRequest request, CancellationToken ct = default)
  {
    Dictionary<string, object> snapshot;
    lock (_lock)
    {
      if (!request.DeviceEnabled)
      {
        _states.Remove(request.Name);
        return Result.Success();
      }

      // Devices report every few seconds, so only a real change is worth broadcasting.
      if (_states.TryGetValue(request.Name, out var existing) && existing.Equals(request.State))
      {
        return Result.Success();
      }

      _states[request.Name] = request.State;
      snapshot = new Dictionary<string, object>(_states);
    }

    await dispatcher.PublishAsync(new DeviceStateChangedEvent(snapshot), ct);
    return Result.Success();
  }

  public Task<Result<GetDeviceStatesResponse>> HandleAsync(GetDeviceStatesRequest request, CancellationToken ct = default)
  {
    lock (_lock)
    {
      return Task.FromResult(Result<GetDeviceStatesResponse>.Success(new GetDeviceStatesResponse(new Dictionary<string, object>(_states))));
    }
  }
}

public class GetDeviceStatesEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet(
        "/api/devices/state",
        async (IHandler<GetDeviceStatesRequest, GetDeviceStatesResponse> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new GetDeviceStatesRequest(), ct);
          return Results.Ok(result.Value!.States);
        }
      )
      .WithName("GetDeviceStates")
      .WithTags("Devices");
  }
}

public class DeviceStateRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddSingleton<DeviceStateHandler>();
    services.AddSingleton<IHandler<UpdateDeviceStateRequest>>(sp => sp.GetRequiredService<DeviceStateHandler>());
    services.AddSingleton<IHandler<GetDeviceStatesRequest, GetDeviceStatesResponse>>(sp => sp.GetRequiredService<DeviceStateHandler>());
  }
}
