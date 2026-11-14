using Kelvin.Server.Application;
using Kelvin.Server.Models;

namespace Kelvin.Server.Features.Control;

public record RestoreCallClockRequest : IRequest<RestoreCallClockResponse?>;

public record RestoreCallClockResponse(
  DateTimeOffset LastCallChangeAt,
  DateTimeOffset LastCoolingEndedAt,
  DateTimeOffset LastHeatingEndedAt,
  GetLatestControlStateChangeResponse? InterruptedCall
);

/// <summary>Recovers the call and mode-switch clocks from recorded history after relay initialization.</summary>
public class RestoreCallClockHandler(IDispatcher dispatcher, TimeProvider time)
  : IHandler<RestoreCallClockRequest, RestoreCallClockResponse?>
{
  public async Task<Result<RestoreCallClockResponse?>> HandleAsync(
    RestoreCallClockRequest request,
    CancellationToken cancellationToken = default
  )
  {
    var latestResult = await dispatcher.DispatchAsync<GetLatestControlStateChangeRequest, GetLatestControlStateChangeResponse?>(
      new(ControlChangeKind.Call),
      cancellationToken
    );
    latestResult.EnsureSuccess();
    var latest = latestResult.Value;
    if (latest is null)
      return Result<RestoreCallClockResponse?>.Success(null);

    var coolingResult = await dispatcher.DispatchAsync<GetControlStateEndTimeRequest, GetControlStateEndTimeResponse>(
      new(ControlState.Cooling),
      cancellationToken
    );
    coolingResult.EnsureSuccess();

    var heatingResult = await dispatcher.DispatchAsync<GetControlStateEndTimeRequest, GetControlStateEndTimeResponse>(
      new(ControlState.Heating),
      cancellationToken
    );
    heatingResult.EnsureSuccess();

    var coolingEndedAt = coolingResult.Value!.ChangedAt ?? DateTimeOffset.MinValue;
    var heatingEndedAt = heatingResult.Value!.ChangedAt ?? DateTimeOffset.MinValue;
    if (latest.State == ControlState.Dwell)
      return Result<RestoreCallClockResponse?>.Success(new(latest.ChangedAt, coolingEndedAt, heatingEndedAt, null));

    // Initialization released the relays, so an interrupted call ended at startup.
    var now = time.GetUtcNow();
    if (latest.State == ControlState.Heating)
      heatingEndedAt = now;
    else if (latest.State == ControlState.Cooling)
      coolingEndedAt = now;

    return Result<RestoreCallClockResponse?>.Success(new(now, coolingEndedAt, heatingEndedAt, latest));
  }
}

public class RestoreCallClockRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<RestoreCallClockRequest, RestoreCallClockResponse?>, RestoreCallClockHandler>();
  }
}
