using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Features.GeoCoding;
using Kelvin.Server.Features.Weather;
using Kelvin.Server.Integration.Weather;
using Kelvin.Server.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Weather;

/// <summary>
/// Tests for <see cref="GetWeatherForecastHandler"/>. The upstream current-location lookup is faked directly
/// (the handler depends on the handler interface, not a concrete class), isolating this from the database.
/// </summary>
public class GetWeatherForecastTests
{
    private static GetCurrentLocationResponse CreateLocation(
        double latitude = 40.7128,
        double longitude = -74.0060
    ) =>
        new(
            Id: 2459115,
            Name: "New York",
            Latitude: latitude,
            Longitude: longitude,
            Elevation: null,
            TimeZone: "America/New_York",
            Country: null,
            CountryCode: null,
            Admin1: null,
            Admin2: null,
            Admin3: null,
            PostCodes: []
        );

    private static WeatherForecast CreateForecast(double latitude, double longitude) =>
        new()
        {
            Latitude = latitude,
            Longitude = longitude,
            TimeZone = "America/New_York",
            RetrievedAt = DateTimeOffset.UtcNow,
            Current = null,
            Daily = [],
        };

    [Fact]
    public async Task LocationHandlerFails_PropagatesTheSameError()
    {
        var locationError = new Error("GetCurrentLocation.LocationNotConfigured", "not configured");
        var locationHandler = A.Fake<
            IHandler<GetCurrentLocationRequest, GetCurrentLocationResponse>
        >();
        A.CallTo(() =>
                locationHandler.HandleAsync(A<GetCurrentLocationRequest>._, A<CancellationToken>._)
            )
            .Returns(Result<GetCurrentLocationResponse>.Failure(locationError));

        var weatherApi = A.Fake<IWeatherApi>();
        var logger = A.Fake<ILogger<GetWeatherForecastHandler>>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new GetWeatherForecastHandler(locationHandler, logger, cache, weatherApi);

        var result = await handler.HandleAsync(new GetWeatherForecastRequest());

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(locationError);
    }

    [Fact]
    public async Task ForecastApiReturnsNull_ReturnsNotFound()
    {
        var location = CreateLocation();
        var locationHandler = A.Fake<
            IHandler<GetCurrentLocationRequest, GetCurrentLocationResponse>
        >();
        A.CallTo(() =>
                locationHandler.HandleAsync(A<GetCurrentLocationRequest>._, A<CancellationToken>._)
            )
            .Returns(Result<GetCurrentLocationResponse>.Success(location));

        var weatherApi = A.Fake<IWeatherApi>();
        A.CallTo(() =>
                weatherApi.GetForecastAsync(
                    location.Latitude,
                    location.Longitude,
                    A<CancellationToken>._
                )
            )
            .Returns(Task.FromResult<WeatherForecast?>(null));

        var logger = A.Fake<ILogger<GetWeatherForecastHandler>>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new GetWeatherForecastHandler(locationHandler, logger, cache, weatherApi);

        var result = await handler.HandleAsync(new GetWeatherForecastRequest());

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GetWeatherForecastErrors.ForecastNotFound);
    }

    [Fact]
    public async Task UnexpectedException_ReturnsDefaultError()
    {
        var location = CreateLocation();
        var locationHandler = A.Fake<
            IHandler<GetCurrentLocationRequest, GetCurrentLocationResponse>
        >();
        A.CallTo(() =>
                locationHandler.HandleAsync(A<GetCurrentLocationRequest>._, A<CancellationToken>._)
            )
            .Returns(Result<GetCurrentLocationResponse>.Success(location));

        var weatherApi = A.Fake<IWeatherApi>();
        A.CallTo(() =>
                weatherApi.GetForecastAsync(
                    location.Latitude,
                    location.Longitude,
                    A<CancellationToken>._
                )
            )
            .Throws(new InvalidOperationException("boom"));

        var logger = A.Fake<ILogger<GetWeatherForecastHandler>>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new GetWeatherForecastHandler(locationHandler, logger, cache, weatherApi);

        var result = await handler.HandleAsync(new GetWeatherForecastRequest());

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GetWeatherForecastErrors.DefaultError);
    }

    [Fact]
    public async Task SuccessfulForecast_ReturnsMappedResponseAndCachesByCoordinates()
    {
        var location = CreateLocation();
        var locationHandler = A.Fake<
            IHandler<GetCurrentLocationRequest, GetCurrentLocationResponse>
        >();
        A.CallTo(() =>
                locationHandler.HandleAsync(A<GetCurrentLocationRequest>._, A<CancellationToken>._)
            )
            .Returns(Result<GetCurrentLocationResponse>.Success(location));

        var forecast = CreateForecast(location.Latitude, location.Longitude);
        var weatherApi = A.Fake<IWeatherApi>();
        A.CallTo(() =>
                weatherApi.GetForecastAsync(
                    location.Latitude,
                    location.Longitude,
                    A<CancellationToken>._
                )
            )
            .Returns(Task.FromResult<WeatherForecast?>(forecast));

        var logger = A.Fake<ILogger<GetWeatherForecastHandler>>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new GetWeatherForecastHandler(locationHandler, logger, cache, weatherApi);

        var firstResult = await handler.HandleAsync(new GetWeatherForecastRequest());
        firstResult.IsSuccess.ShouldBeTrue();
        firstResult.Value.ShouldNotBeNull().TimeZone.ShouldBe("America/New_York");

        var secondResult = await handler.HandleAsync(new GetWeatherForecastRequest());
        secondResult.IsSuccess.ShouldBeTrue();

        A.CallTo(() =>
                weatherApi.GetForecastAsync(
                    location.Latitude,
                    location.Longitude,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DifferentCoordinates_BypassTheCache()
    {
        var firstLocation = CreateLocation(40.7128, -74.0060);
        var secondLocation = CreateLocation(34.0522, -118.2437);

        var locationHandler = A.Fake<
            IHandler<GetCurrentLocationRequest, GetCurrentLocationResponse>
        >();
        A.CallTo(() =>
                locationHandler.HandleAsync(A<GetCurrentLocationRequest>._, A<CancellationToken>._)
            )
            .ReturnsNextFromSequence(
                Result<GetCurrentLocationResponse>.Success(firstLocation),
                Result<GetCurrentLocationResponse>.Success(secondLocation)
            );

        var weatherApi = A.Fake<IWeatherApi>();
        A.CallTo(() =>
                weatherApi.GetForecastAsync(A<double>._, A<double>._, A<CancellationToken>._)
            )
            .ReturnsLazily(
                (double lat, double lng, CancellationToken _) =>
                    Task.FromResult<WeatherForecast?>(CreateForecast(lat, lng))
            );

        var logger = A.Fake<ILogger<GetWeatherForecastHandler>>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new GetWeatherForecastHandler(locationHandler, logger, cache, weatherApi);

        await handler.HandleAsync(new GetWeatherForecastRequest());
        await handler.HandleAsync(new GetWeatherForecastRequest());

        A.CallTo(() =>
                weatherApi.GetForecastAsync(A<double>._, A<double>._, A<CancellationToken>._)
            )
            .MustHaveHappenedTwiceExactly();
    }
}
