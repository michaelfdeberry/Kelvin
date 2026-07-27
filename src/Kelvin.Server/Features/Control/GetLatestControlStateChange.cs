using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Control;

public record GetLatestControlStateChangeRequest(ControlChangeKind Kind) : IRequest<GetLatestControlStateChangeResponse?>;

public record GetLatestControlStateChangeResponse(ControlState State, DateTimeOffset ChangedAt);

/// <summary>
/// Reads the latest recorded change for a specific state axis.
/// </summary>
public class GetLatestControlStateChangeHandler(KelvinContext context)
  : IHandler<GetLatestControlStateChangeRequest, GetLatestControlStateChangeResponse?>
{
  public async Task<Result<GetLatestControlStateChangeResponse?>> HandleAsync(
    GetLatestControlStateChangeRequest request,
    CancellationToken cancellationToken = default
  )
  {
    var change = await context
      .ControlStateChanges.AsNoTracking()
      .Where(change => change.Kind == request.Kind && change.DeletedAt == null)
      .OrderByDescending(change => change.CreatedAt)
      .Select(change => new GetLatestControlStateChangeResponse(change.State, change.CreatedAt))
      .FirstOrDefaultAsync(cancellationToken);

    return Result<GetLatestControlStateChangeResponse?>.Success(change);
  }
}

public class GetLatestControlStateChangeFeatureRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<GetLatestControlStateChangeRequest, GetLatestControlStateChangeResponse?>, GetLatestControlStateChangeHandler>();
  }
}
