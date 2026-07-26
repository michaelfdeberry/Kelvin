using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Control;

public record GetControlStateRequest() : IRequest<GetControlStateResponse>;

/// <summary>
/// What the equipment is doing right now, assembled from the latest change on each independent state axis.
/// </summary>
public record GetControlStateResponse(
  ControlState ControlState,
  DateTimeOffset? ControlSince,
  ControlState CallState,
  DateTimeOffset? CallSince,
  bool FanOn,
  DateTimeOffset? FanSince,
  ControlStateChangeDto? LastChange
);

/// <summary>
/// Reads the current control state back out of the recorded history.
/// </summary>
/// <remarks>
/// Deliberately not cached: this is the live status endpoint, and a stale answer about whether the furnace is
/// running is worse than no answer. Each axis is a single indexed lookup of its most recent row.
/// <para>
/// Until the control service has actuated anything, there is no history to read and the defaults describe the
/// failsafe state the hardware powers up in: control reverted to the legacy thermostat, idle, fan off.
/// </para>
/// </remarks>
public class GetControlStateHandler(KelvinContext context) : IHandler<GetControlStateRequest, GetControlStateResponse>
{
  public async Task<Result<GetControlStateResponse>> HandleAsync(GetControlStateRequest request, CancellationToken cancellationToken = default)
  {
    var control = await LatestAsync(ControlChangeKind.Control, cancellationToken);
    var call = await LatestAsync(ControlChangeKind.Call, cancellationToken);
    var fan = await LatestAsync(ControlChangeKind.Fan, cancellationToken);

    var lastChange = new[] { control, call, fan }.Where(change => change is not null).MaxBy(change => change!.CreatedAt);

    var response = new GetControlStateResponse(
      ControlState: control?.State ?? ControlState.Disable,
      ControlSince: control?.CreatedAt,
      CallState: call?.State ?? ControlState.Dwell,
      CallSince: call?.CreatedAt,
      FanOn: fan?.State == ControlState.FanOn,
      FanSince: fan?.CreatedAt,
      LastChange: lastChange is null ? null : ControlStateChangeDto.FromEntity(lastChange)
    );

    return Result<GetControlStateResponse>.Success(response);
  }

  private Task<ControlStateChange?> LatestAsync(ControlChangeKind kind, CancellationToken cancellationToken) =>
    context
      .ControlStateChanges.AsNoTracking()
      .Where(change => change.Kind == kind && change.DeletedAt == null)
      .OrderByDescending(change => change.CreatedAt)
      .FirstOrDefaultAsync(cancellationToken);
}

public class GetControlStateEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet(
        "/api/control/state",
        async (IHandler<GetControlStateRequest, GetControlStateResponse> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new GetControlStateRequest(), ct);
          if (result.IsFailure)
            return Results.InternalServerError(result.Error);

          return Results.Ok(result.Value);
        }
      )
      .WithName("GetControlState")
      .WithTags("Control");
  }
}

public class GetControlStateFeatureRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<GetControlStateRequest, GetControlStateResponse>, GetControlStateHandler>();
  }
}
