using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.Humidifiers;

public record UpdateHumidifierConfigurationRequest(Guid Id, HumidifierConfigurationRequest Update) : IRequest;

public static class UpdateHumidifierConfigurationErrors
{
  public static readonly Error NotFoundError = new("UpdateHumidifierConfiguration.NotFound", "The requested humidifier was not found.");
  public static readonly Error InvalidMonth = new("UpdateHumidifierConfiguration.InvalidMonth", "The season months must be between 1 and 12.");
  public static readonly Error InvalidTargets = new(
    "UpdateHumidifierConfiguration.InvalidTargets",
    "Targets must be unique by outdoor temperature and have a humidity between 0 and 100."
  );
}

public class UpdateHumidifierConfigurationHandler(KelvinContext context, IDispatcher dispatcher) : IHandler<UpdateHumidifierConfigurationRequest>
{
  public async Task<Result> HandleAsync(UpdateHumidifierConfigurationRequest request, CancellationToken ct = default)
  {
    var update = request.Update;
    if (update.StartMonth is < 1 or > 12 || update.EndMonth is < 1 or > 12)
    {
      return Result.Failure(UpdateHumidifierConfigurationErrors.InvalidMonth);
    }

    var targets = update.Targets ?? [];
    var targetsAreValid =
      targets.All(t => float.IsFinite(t.OutdoorTemperatureC) && float.IsFinite(t.HumidityPercentage) && t.HumidityPercentage is >= 0 and <= 100)
      && targets.Select(t => t.OutdoorTemperatureC).Distinct().Count() == targets.Count;
    if (!targetsAreValid)
    {
      return Result.Failure(UpdateHumidifierConfigurationErrors.InvalidTargets);
    }

    var device = await context.Devices.OfType<HumidifierDevice>().FirstOrDefaultAsync(d => d.Id == request.Id && d.DeletedAt == null, ct);
    if (device is null)
    {
      return Result.Failure(UpdateHumidifierConfigurationErrors.NotFoundError);
    }

    device.Configuration = new HumidifierConfiguration
    {
      StartMonth = update.StartMonth,
      EndMonth = update.EndMonth,
      Targets = targets.OrderBy(t => t.OutdoorTemperatureC).ToList(),
    };
    await context.SaveChangesAsync(ct);

    await dispatcher.PublishAsync(new DevicesChangedEvent(), ct);
    return Result.Success();
  }
}

public class UpdateHumidifierConfigurationEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPut(
        "/api/humidifiers/{id:guid}/configuration",
        async (Guid id, HumidifierConfigurationRequest update, IHandler<UpdateHumidifierConfigurationRequest> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new UpdateHumidifierConfigurationRequest(id, update), ct);
          if (result.IsFailure)
          {
            if (result.Error == UpdateHumidifierConfigurationErrors.NotFoundError)
            {
              return Results.NotFound(result.Error);
            }

            return Results.BadRequest(result.Error);
          }

          return Results.NoContent();
        }
      )
      .WithName("UpdateHumidifierConfiguration")
      .WithTags("Humidifiers");
  }
}

public class UpdateHumidifierConfigurationRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<UpdateHumidifierConfigurationRequest>, UpdateHumidifierConfigurationHandler>();
  }
}
