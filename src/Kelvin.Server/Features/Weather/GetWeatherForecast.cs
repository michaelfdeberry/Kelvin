using Kelvin.Server.Application;
using Kelvin.Server.Features.GeoCoding;
using Kelvin.Server.Integration.Weather;
using Kelvin.Server.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Kelvin.Server.Features.Weather;

public record GetWeatherForecastRequest() : IRequest<GetWeatherForecastResponse>;

public record GetWeatherForecastResponse(
  double Latitude,
  double Longitude,
  string TimeZone,
  DateTimeOffset RetrievedAt,
  WeatherCurrent? Current,
  IEnumerable<WeatherForecastDay> Daily
);

public static class GetWeatherForecastErrors
{
  public static readonly Error ForecastNotFound = new("GetWeatherForecast.ForecastNotFound", "The weather forecast was not found.");
  public static readonly Error DefaultError = new("GetWeatherForecast.Failed", "An error occurred processing the request.");
}

public class GetWeatherForecastHandler(
  IHandler<GetCurrentLocationRequest, GetCurrentLocationResponse> currentLocationHandler,
  ILogger<GetWeatherForecastHandler> logger,
  IMemoryCache cache,
  IWeatherApi weatherApi
) : IHandler<GetWeatherForecastRequest, GetWeatherForecastResponse>
{
  public async Task<Result<GetWeatherForecastResponse>> HandleAsync(GetWeatherForecastRequest request, CancellationToken ct = default)
  {
    try
    {
      var locationResult = await currentLocationHandler.HandleAsync(new GetCurrentLocationRequest(), ct);
      if (!locationResult.IsSuccess)
      {
        return Result<GetWeatherForecastResponse>.Failure(locationResult.Error);
      }

      var location = locationResult.Value!;

      var cacheKey = $"{"weather-forecast"}:{location.Latitude:F4}:{location.Longitude:F4}";
      if (cache.TryGetValue(cacheKey, out GetWeatherForecastResponse? cachedResponse) && cachedResponse is not null)
      {
        return Result<GetWeatherForecastResponse>.Success(cachedResponse);
      }

      var forecast = await weatherApi.GetForecastAsync(location.Latitude, location.Longitude, ct);
      if (forecast is null)
      {
        return Result<GetWeatherForecastResponse>.Failure(GetWeatherForecastErrors.ForecastNotFound);
      }

      var response = new GetWeatherForecastResponse(
        forecast.Latitude,
        forecast.Longitude,
        forecast.TimeZone,
        forecast.RetrievedAt,
        forecast.Current,
        forecast.Daily
      );

      cache.Set(cacheKey, response, TimeSpan.FromHours(1));
      return Result<GetWeatherForecastResponse>.Success(response);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "An error occurred while getting the weather forecast.");
      return Result<GetWeatherForecastResponse>.Failure(GetWeatherForecastErrors.DefaultError);
    }
  }
}

public class GetWeatherForecastEndpoint : IEndpointMapper
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet(
        "/api/weather/forecast",
        async (IHandler<GetWeatherForecastRequest, GetWeatherForecastResponse> handler, CancellationToken ct) =>
        {
          var result = await handler.HandleAsync(new GetWeatherForecastRequest(), ct);
          if (result.IsFailure)
          {
            if (result.Error == GetWeatherForecastErrors.ForecastNotFound)
              return Results.NotFound(result.Error);

            return Results.InternalServerError(result.Error);
          }

          return Results.Ok(result.Value);
        }
      )
      .WithName("GetWeatherForecast")
      .WithTags("Weather");
  }
}

public class GetWeatherForecastFeatureRegistration : IRegistration
{
  public void Register(IServiceCollection services)
  {
    services.AddScoped<IHandler<GetWeatherForecastRequest, GetWeatherForecastResponse>, GetWeatherForecastHandler>();
  }
}
