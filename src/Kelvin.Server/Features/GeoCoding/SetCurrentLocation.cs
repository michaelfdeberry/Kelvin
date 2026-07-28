using Kelvin.Server.Application;
using Kelvin.Server.Data;
using Kelvin.Server.Integration.GeoCoding;
using Microsoft.EntityFrameworkCore;

namespace Kelvin.Server.Features.GeoCoding;

public record SetCurrentLocationRequest(long LocationId) : IRequest;

public static class SetCurrentLocationErrors
{
  public static readonly Error InvalidLocation = new("SetCurrentLocation.InvalidLocation", "The location identifier is invalid.");
  public static readonly Error LocationNotFound = new("SetCurrentLocation.LocationNotFound", "The selected location was not found.");
  public static readonly Error DefaultError = new("SetCurrentLocation.Failed", "An error occurred processing the request.");
}

public class SetCurrentLocationHandler(KelvinContext context, IGeoCodingApi geoCodingApi, ILogger<SetCurrentLocationHandler> logger)
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

public class SetCurrentLocationFeatureRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<SetCurrentLocationRequest>, SetCurrentLocationHandler>();
  }
}
