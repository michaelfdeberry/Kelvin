using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Control;

public record GetControlStateEndTimeRequest(ControlState State) : IRequest<GetControlStateEndTimeResponse>;

public record GetControlStateEndTimeResponse(DateTimeOffset? ChangedAt);

public class GetControlStateEndTimeHandler(KelvinContext context) : IHandler<GetControlStateEndTimeRequest, GetControlStateEndTimeResponse>
{
  public async Task<Result<GetControlStateEndTimeResponse>> HandleAsync(GetControlStateEndTimeRequest request, CancellationToken ct = default)
  {
    var change = await context
      .ControlStateChanges.Where(change => change.State == ControlState.Dwell && change.PreviousState == request.State && change.DeletedAt == null)
      .OrderByDescending(change => change.CreatedAt)
      .FirstOrDefaultAsync(ct);

    var response = new GetControlStateEndTimeResponse(change?.CreatedAt);
    return Result<GetControlStateEndTimeResponse>.Success(response);
  }
}

public class GetControlStateEndTimeRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<GetControlStateEndTimeRequest, GetControlStateEndTimeResponse>, GetControlStateEndTimeHandler>();
  }
}
