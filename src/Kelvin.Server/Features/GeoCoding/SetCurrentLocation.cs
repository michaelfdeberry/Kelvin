using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Features.Weather;
using Kelvin.Server.Integration.GeoCoding;
using Kelvin.Server.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.GeoCoding;

public record SetCurrentLocationRequest(long LocationId) : IRequest;

public static class SetCurrentLocationErrors
{
  public static readonly Error InvalidLocation = new("SetCurrentLocation.InvalidLocation", "The location identifier is invalid.");
  public static readonly Error LocationNotFound = new("SetCurrentLocation.LocationNotFound", "The selected location was not found.");
  public static readonly Error DefaultError = new("SetCurrentLocation.Failed", "An error occurred processing the request.");
}

public class SetCurrentLocationHandler(KelvinContext context, IGeoCodingApi geoCodingApi, IDispatcher dispatcher, ILogger<SetCurrentLocationHandler> logger)
  : IHandler<SetCurrentLocationRequest>
{
  public async Task<Result> HandleAsync(SetCurrentLocationRequest request, CancellationToken ct = default)
  {
    try
    {
      if (request.LocationId <= 0)
      {
        return Result.Failure(SetCurrentLocationErrors.InvalidLocation);
      }

      var location = await geoCodingApi.GetByIdAsync(request.LocationId, ct);
      if (location is null)
      {
        return Result.Failure(SetCurrentLocationErrors.LocationNotFound);
      }

      var preferences = await context.Preferences.FirstOrDefaultAsync(ct);
      if (preferences is null)
      {
        preferences = new Models.Preferences();
        context.Preferences.Add(preferences);
      }

      preferences.LocationId = location.Id;
      preferences.LocationName = location.Name;

      await context.SaveChangesAsync(ct);
      await dispatcher.PublishAsync(new PreferencesChangedEvent(preferences), ct);
      await PushForecastAsync(ct);

      return Result.Success();
    }
    catch (GeoCodingException ex)
    {
      logger.LogError(ex, "An error occurred while setting the current location.");
      return Result.Failure(SetCurrentLocationErrors.LocationNotFound);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "An unexpected error occurred while setting the current location.");
      return Result.Failure(SetCurrentLocationErrors.DefaultError);
    }
  }

  // The forecast handler only pushes what it fetches, so a forecast for the new location that is already cached has to be pushed here.
  // The location is already saved, so a forecast that can't be retrieved is left for the periodic refresh rather than failing the request.
  private async Task PushForecastAsync(CancellationToken ct)
  {
    try
    {
      var forecast = await dispatcher.DispatchAsync<GetWeatherForecastRequest, GetWeatherForecastResponse>(new GetWeatherForecastRequest(), ct);
      if (forecast.IsSuccess)
        await dispatcher.PublishAsync(new WeatherForecastChangedEvent(forecast.Value!), ct);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      logger.LogWarning(ex, "Unable to push the forecast for the new location.");
    }
  }
}

public class SetCurrentLocationEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPut(
        "/api/locations/current",
        async (SetCurrentLocationRequest request, IHandler<SetCurrentLocationRequest> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(request, ct);
          if (result.IsFailure)
          {
            if (result.Error == SetCurrentLocationErrors.InvalidLocation)
              return Results.BadRequest(result.Error);

            if (result.Error == SetCurrentLocationErrors.LocationNotFound)
              return Results.NotFound(result.Error);

            return Results.InternalServerError(result.Error);
          }

          return Results.NoContent();
        }
      )
      .WithName("SetCurrentLocation")
      .WithTags("Locations");
  }
}

public class SetCurrentLocationRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<SetCurrentLocationRequest>, SetCurrentLocationHandler>();
  }
}
