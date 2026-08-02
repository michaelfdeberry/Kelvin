using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Control;

public record GetControlStatsRequest(DateTimeOffset? From = null, DateTimeOffset? To = null) : IRequest<GetControlStatsResponse>;

/// <summary>
/// How much the equipment ran over a window, and how often it cycled.
/// </summary>
public record GetControlStatsResponse(
  DateTimeOffset From,
  DateTimeOffset To,
  double HeatingSeconds,
  double CoolingSeconds,
  double DwellSeconds,
  double ControlledSeconds,
  double RevertedSeconds,
  double FanSeconds,
  int HeatingCycles,
  int CoolingCycles,
  double? AverageHeatingCycleSeconds,
  double? AverageCoolingCycleSeconds
);

public static class GetControlStatsErrors
{
  public static readonly Error InvalidRange = new("GetControlStats.InvalidRange", "The start of the range must not be after the end of it.");
}

public class GetControlStatsHandler(KelvinContext context, TimeProvider time) : IHandler<GetControlStatsRequest, GetControlStatsResponse>
{
  private static readonly TimeSpan DefaultWindow = TimeSpan.FromHours(24);

  public async Task<Result<GetControlStatsResponse>> HandleAsync(GetControlStatsRequest request, CancellationToken cancellationToken = default)
  {
    if (request.From is not null && request.To is not null && request.From > request.To)
      return Result<GetControlStatsResponse>.Failure(GetControlStatsErrors.InvalidRange);

    var now = time.GetUtcNow();
    var to = request.To ?? now;
    var from = request.From ?? to - DefaultWindow;

    var callSpans = await BuildTimelineAsync(ControlChangeKind.Call, ControlState.Dwell, from, to, now, cancellationToken);
    var controlSpans = await BuildTimelineAsync(ControlChangeKind.Control, ControlState.Disable, from, to, now, cancellationToken);
    var fanSpans = await BuildTimelineAsync(ControlChangeKind.Fan, ControlState.FanOff, from, to, now, cancellationToken);

    var heatingCycles = await CountTransitionsIntoAsync(ControlState.Heating, from, to, cancellationToken);
    var coolingCycles = await CountTransitionsIntoAsync(ControlState.Cooling, from, to, cancellationToken);

    var heatingSeconds = callSpans.GetValueOrDefault(ControlState.Heating);
    var coolingSeconds = callSpans.GetValueOrDefault(ControlState.Cooling);

    var response = new GetControlStatsResponse(
      From: from,
      To: to,
      HeatingSeconds: heatingSeconds,
      CoolingSeconds: coolingSeconds,
      DwellSeconds: callSpans.GetValueOrDefault(ControlState.Dwell),
      ControlledSeconds: controlSpans.GetValueOrDefault(ControlState.Enable),
      RevertedSeconds: controlSpans.GetValueOrDefault(ControlState.Disable),
      FanSeconds: fanSpans.GetValueOrDefault(ControlState.FanOn),
      HeatingCycles: heatingCycles,
      CoolingCycles: coolingCycles,
      AverageHeatingCycleSeconds: heatingCycles == 0 ? null : heatingSeconds / heatingCycles,
      AverageCoolingCycleSeconds: coolingCycles == 0 ? null : coolingSeconds / coolingCycles
    );

    return Result<GetControlStatsResponse>.Success(response);
  }

  /// <summary>
  /// Totals how long each state on one axis was held inside the window, clipping the spans at both edges.
  /// </summary>
  private async Task<Dictionary<ControlState, double>> BuildTimelineAsync(
    ControlChangeKind kind,
    ControlState defaultState,
    DateTimeOffset from,
    DateTimeOffset to,
    DateTimeOffset now,
    CancellationToken cancellationToken
  )
  {
    var query = context.ControlStateChanges.AsNoTracking().Where(change => change.Kind == kind && change.DeletedAt == null);

    // TODO: this throws an exception if there are no changes at all in the database.
    // Whatever was already running when the window opened; without it the leading span would be lost.
    var openingState =
      await query
        .Where(change => change.CreatedAt <= from)
        .OrderByDescending(change => change.CreatedAt)
        .Select(change => (ControlState?)change.State)
        .FirstOrDefaultAsync(cancellationToken)
      ?? defaultState;

    var changes = await query
      .Where(change => change.CreatedAt > from && change.CreatedAt <= to)
      .OrderBy(change => change.CreatedAt)
      .Select(change => new { change.CreatedAt, change.State })
      .ToListAsync(cancellationToken);

    var totals = new Dictionary<ControlState, double>();
    var spanStart = from;
    var spanState = openingState;

    foreach (var change in changes)
    {
      Accumulate(totals, spanState, change.CreatedAt - spanStart);
      spanStart = change.CreatedAt;
      spanState = change.State;
    }

    // The final state is still running, so it only counts up to the end of the window or to now, whichever is first.
    Accumulate(totals, spanState, Min(to, now) - spanStart);

    return totals;
  }

  private Task<int> CountTransitionsIntoAsync(ControlState state, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
    context
      .ControlStateChanges.AsNoTracking()
      .Where(change => change.DeletedAt == null && change.State == state && change.CreatedAt > from && change.CreatedAt <= to)
      .CountAsync(cancellationToken);

  private static void Accumulate(Dictionary<ControlState, double> totals, ControlState state, TimeSpan duration)
  {
    if (duration <= TimeSpan.Zero)
      return;

    totals[state] = totals.GetValueOrDefault(state) + duration.TotalSeconds;
  }

  private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left < right ? left : right;
}

public class GetControlStatsEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet(
        "/api/control/stats",
        async (
          IHandler<GetControlStatsRequest, GetControlStatsResponse> handler,
          CancellationToken ct,
          [FromQuery] DateTimeOffset? from = null,
          [FromQuery] DateTimeOffset? to = null
        ) =>
        {
          var result = await handler.HandleAsync(new GetControlStatsRequest(from, to), ct);
          if (result.IsFailure)
          {
            if (result.Error == GetControlStatsErrors.InvalidRange)
              return Results.BadRequest(result.Error);

            return Results.InternalServerError(result.Error);
          }

          return Results.Ok(result.Value);
        }
      )
      .WithName("GetControlStats")
      .WithTags("Control");
  }
}

public class GetControlStatsFeatureRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<GetControlStatsRequest, GetControlStatsResponse>, GetControlStatsHandler>();
  }
}
