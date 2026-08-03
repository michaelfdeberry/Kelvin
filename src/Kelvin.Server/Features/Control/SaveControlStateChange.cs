using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Hubs;
using Kelvin.Server.Models;
using Microsoft.AspNetCore.SignalR;

namespace Kelvin.Server.Features.Control;

public record SaveControlStateChangeRequest(ControlStateChange Change) : IRequest;

public static class SaveControlStateChangeErrors
{
  public static readonly Error DefaultError = new("SaveControlStateChange.Failed", "The control state change could not be recorded.");
}

/// <summary>
/// Records a control state change and announces it.
/// </summary>
/// <remarks>
/// This is the fan-out point for everything that reacts to a state change. The row is committed first, then the
/// change is announced, so a subscriber can never be told about something that was not persisted. Notifications
/// will be dispatched here alongside the broadcast.
/// </remarks>
public class SaveControlStateChangeHandler(
  KelvinContext context,
  IHubContext<ControlHub, IControlClient> hub,
  ILogger<SaveControlStateChangeHandler> logger
) : IHandler<SaveControlStateChangeRequest>
{
  public async Task<Result> HandleAsync(SaveControlStateChangeRequest request, CancellationToken cancellationToken = default)
  {
    try
    {
      context.ControlStateChanges.Add(request.Change);
      await context.SaveChangesAsync(cancellationToken);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Failed to record the {Kind} state change to {State}.", request.Change.Kind, request.Change.State);
      return Result.Failure(SaveControlStateChangeErrors.DefaultError);
    }

    try
    {
      await hub.Clients.All.ControlStateChanged(ControlStateChangeDto.FromEntity(request.Change));
    }
    catch (Exception ex)
    {
      // The change is already recorded, so a client that missed the broadcast can still read the current state.
      logger.LogError(ex, "Failed to broadcast the {Kind} state change to {State}.", request.Change.Kind, request.Change.State);
    }

    return Result.Success();
  }
}

public class SaveControlStateChangeRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<SaveControlStateChangeRequest>, SaveControlStateChangeHandler>();
  }
}
