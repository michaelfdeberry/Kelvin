using Kelvin.Server.Application;
using Kelvin.Server.Features.Preferences;
using Kelvin.Server.Integration.GeoCoding;
using Microsoft.Extensions.Caching.Memory;

namespace Kelvin.Server.Features.GeoCoding;

public record GetCurrentLocationRequest() : IRequest<GetCurrentLocationResponse>;

public record GetCurrentLocationResponse(
  long Id,
  string Name,
  double Latitude,
  double Longitude,
  double? Elevation,
  string? TimeZone,
  string? Country,
  string? CountryCode,
  string? Admin1,
  string? Admin2,
  string? Admin3,
  IReadOnlyList<string> PostCodes
);

public static class GetCurrentLocationErrors
{
  public static readonly Error LocationNotConfigured = new("GetCurrentLocation.LocationNotConfigured", "The current location is not configured.");
  public static readonly Error LocationNotFound = new("GetCurrentLocation.LocationNotFound", "The current location was not found.");
  public static readonly Error DefaultError = new("GetCurrentLocation.Failed", "An error occurred processing the request.");
}

public class GetCurrentLocationHandler(
  IGeoCodingApi geoCodingApi,
  IHandler<GetPreferencesRequest, GetPreferencesResponse> preferencesHandler,
  ILogger<GetCurrentLocationHandler> logger,
  IMemoryCache cache
) : IHandler<GetCurrentLocationRequest, GetCurrentLocationResponse>
{
  public async Task<Result<GetCurrentLocationResponse>> HandleAsync(GetCurrentLocationRequest request, CancellationToken ct = default)
  {
    try
    {
      var preferencesResult = await preferencesHandler.HandleAsync(new GetPreferencesRequest(), ct);
      if (!preferencesResult.IsSuccess)
      {
        return Result<GetCurrentLocationResponse>.Failure(GetCurrentLocationErrors.DefaultError);
      }

      var preferences = preferencesResult.Value;
      if (preferences?.LocationId is null)
      {
        return Result<GetCurrentLocationResponse>.Failure(GetCurrentLocationErrors.LocationNotConfigured);
      }

      var cacheKey = $"{"current-location"}:{preferences.LocationId.Value}";
      if (cache.TryGetValue(cacheKey, out GetCurrentLocationResponse? cachedResponse) && cachedResponse is not null)
      {
        return Result<GetCurrentLocationResponse>.Success(cachedResponse);
      }

      var location = await geoCodingApi.GetByIdAsync(preferences.LocationId.Value, ct);
      if (location is null)
      {
        return Result<GetCurrentLocationResponse>.Failure(GetCurrentLocationErrors.LocationNotFound);
      }

      var response = new GetCurrentLocationResponse(
        Id: location.Id,
        Name: location.Name,
        Latitude: location.Latitude,
        Longitude: location.Longitude,
        Elevation: location.Elevation,
        TimeZone: location.TimeZone,
        Country: location.Country,
        CountryCode: location.CountryCode,
        Admin1: location.Admin1,
        Admin2: location.Admin2,
        Admin3: location.Admin3,
        PostCodes: location.PostCodes
      );

      cache.Set(cacheKey, response, TimeSpan.FromHours(24));

      return Result<GetCurrentLocationResponse>.Success(response);
    }
    catch (GeoCodingException ex)
    {
      logger.LogError(ex, "An error occurred while retrieving the current location.");
      return Result<GetCurrentLocationResponse>.Failure(GetCurrentLocationErrors.LocationNotFound);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "An unexpected error occurred while retrieving the current location.");
      return Result<GetCurrentLocationResponse>.Failure(GetCurrentLocationErrors.DefaultError);
    }
  }
}

public class GetCurrentLocationEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet(
        "/api/locations/current",
        async (IHandler<GetCurrentLocationRequest, GetCurrentLocationResponse> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new GetCurrentLocationRequest(), ct);
          if (result.IsFailure)
          {
            if (result.Error == GetCurrentLocationErrors.LocationNotConfigured)
              return Results.Json(result.Error, statusCode: StatusCodes.Status412PreconditionFailed);

            if (result.Error == GetCurrentLocationErrors.LocationNotFound)
              return Results.NotFound(result.Error);

            return Results.InternalServerError(result.Error);
          }

          return Results.Ok(result.Value);
        }
      )
      .WithName("GetCurrentLocation")
      .WithTags("Locations");
  }
}

public class GetCurrentLocationRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<GetCurrentLocationRequest, GetCurrentLocationResponse>, GetCurrentLocationHandler>();
  }
}
