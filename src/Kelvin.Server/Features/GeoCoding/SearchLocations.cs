using Kelvin.Server.Application;
using Kelvin.Server.Integration.GeoCoding;

namespace Kelvin.Server.Features.GeoCoding;

public record SearchLocationsRequest(string Query, int Count = 10) : IRequest<SearchLocationsResponse>;

public record SearchLocationsResponse(IReadOnlyList<GetCurrentLocationResponse> Locations);

public static class SearchLocationsErrors
{
  public static readonly Error InvalidQuery = new("SearchLocations.InvalidQuery", "The search query is required.");
  public static readonly Error DefaultError = new("SearchLocations.Failed", "An error occurred processing the request.");
}

public class SearchLocationsHandler(IGeoCodingApi geoCodingApi, ILogger<SearchLocationsHandler> logger)
  : IHandler<SearchLocationsRequest, SearchLocationsResponse>
{
  public async Task<Result<SearchLocationsResponse>> HandleAsync(SearchLocationsRequest request, CancellationToken ct = default)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(request.Query))
      {
        return Result<SearchLocationsResponse>.Failure(SearchLocationsErrors.InvalidQuery);
      }

      var count = Math.Clamp(request.Count, 1, 20);
      var locations = await geoCodingApi.SearchAsync(request.Query, count, ct);

      var response = new SearchLocationsResponse([
        .. locations.Select(location => new GetCurrentLocationResponse(
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
        )),
      ]);

      return Result<SearchLocationsResponse>.Success(response);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "An unexpected error occurred while searching for locations.");
      return Result<SearchLocationsResponse>.Failure(SearchLocationsErrors.DefaultError);
    }
  }
}

public class SearchLocationsEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet(
        "/api/locations/search",
        async (string? query, int? count, IHandler<SearchLocationsRequest, SearchLocationsResponse> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new SearchLocationsRequest(query ?? string.Empty, count ?? 10), ct);
          if (result.IsFailure)
          {
            if (result.Error == SearchLocationsErrors.InvalidQuery)
              return Results.BadRequest(result.Error);

            return Results.InternalServerError(result.Error);
          }

          return Results.Ok(result.Value);
        }
      )
      .WithName("SearchLocations")
      .WithTags("Locations");
  }
}

public class SearchLocationsFeatureRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<SearchLocationsRequest, SearchLocationsResponse>, SearchLocationsHandler>();
  }
}
