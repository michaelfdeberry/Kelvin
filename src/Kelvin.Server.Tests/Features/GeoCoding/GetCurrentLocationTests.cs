using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Features.GeoCoding;
using Kelvin.Server.Features.Preferences;
using Kelvin.Server.Integration.GeoCoding;
using Kelvin.Server.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.GeoCoding;

/// <summary>
/// Tests for <see cref="GetCurrentLocationHandler"/>. The upstream preferences lookup is faked directly (the
/// handler depends on the handler interface, not a concrete class), which keeps this isolated from the database.
/// </summary>
public class GetCurrentLocationTests
{
    private static GetPreferencesResponse PreferencesWithLocation(long? locationId) =>
        new(TemperatureUnit.Celsius, TimeFormat.Hour12, locationId, null);

    [Fact]
    public async Task LocationNotConfigured_ReturnsFailure()
    {
        var preferencesHandler = A.Fake<IHandler<GetPreferencesRequest, GetPreferencesResponse>>();
        A.CallTo(() =>
                preferencesHandler.HandleAsync(A<GetPreferencesRequest>._, A<CancellationToken>._)
            )
            .Returns(Result<GetPreferencesResponse>.Success(PreferencesWithLocation(null)));

        var geoCodingApi = A.Fake<IGeoCodingApi>();
        var logger = A.Fake<ILogger<GetCurrentLocationHandler>>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new GetCurrentLocationHandler(
            geoCodingApi,
            preferencesHandler,
            logger,
            cache
        );

        var result = await handler.HandleAsync(new GetCurrentLocationRequest());

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GetCurrentLocationErrors.LocationNotConfigured);
    }

    [Fact]
    public async Task PreferencesHandlerFails_ReturnsDefaultError()
    {
        var preferencesHandler = A.Fake<IHandler<GetPreferencesRequest, GetPreferencesResponse>>();
        A.CallTo(() =>
                preferencesHandler.HandleAsync(A<GetPreferencesRequest>._, A<CancellationToken>._)
            )
            .Returns(
                Result<GetPreferencesResponse>.Failure(new Error("Preferences.Failed", "boom"))
            );

        var geoCodingApi = A.Fake<IGeoCodingApi>();
        var logger = A.Fake<ILogger<GetCurrentLocationHandler>>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new GetCurrentLocationHandler(
            geoCodingApi,
            preferencesHandler,
            logger,
            cache
        );

        var result = await handler.HandleAsync(new GetCurrentLocationRequest());

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GetCurrentLocationErrors.DefaultError);
    }

    [Fact]
    public async Task LocationNotFoundFromApi_ReturnsFailure()
    {
        var preferencesHandler = A.Fake<IHandler<GetPreferencesRequest, GetPreferencesResponse>>();
        A.CallTo(() =>
                preferencesHandler.HandleAsync(A<GetPreferencesRequest>._, A<CancellationToken>._)
            )
            .Returns(Result<GetPreferencesResponse>.Success(PreferencesWithLocation(2459115)));

        var geoCodingApi = A.Fake<IGeoCodingApi>();
        A.CallTo(() => geoCodingApi.GetByIdAsync(2459115, A<CancellationToken>._))
            .Returns(Task.FromResult<GeoCodingLocation?>(null));

        var logger = A.Fake<ILogger<GetCurrentLocationHandler>>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new GetCurrentLocationHandler(
            geoCodingApi,
            preferencesHandler,
            logger,
            cache
        );

        var result = await handler.HandleAsync(new GetCurrentLocationRequest());

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GetCurrentLocationErrors.LocationNotFound);
    }

    [Fact]
    public async Task GeoCodingExceptionFromApi_ReturnsLocationNotFound()
    {
        var preferencesHandler = A.Fake<IHandler<GetPreferencesRequest, GetPreferencesResponse>>();
        A.CallTo(() =>
                preferencesHandler.HandleAsync(A<GetPreferencesRequest>._, A<CancellationToken>._)
            )
            .Returns(Result<GetPreferencesResponse>.Success(PreferencesWithLocation(2459115)));

        var geoCodingApi = A.Fake<IGeoCodingApi>();
        A.CallTo(() => geoCodingApi.GetByIdAsync(2459115, A<CancellationToken>._))
            .Throws(new GeoCodingException("boom"));

        var logger = A.Fake<ILogger<GetCurrentLocationHandler>>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new GetCurrentLocationHandler(
            geoCodingApi,
            preferencesHandler,
            logger,
            cache
        );

        var result = await handler.HandleAsync(new GetCurrentLocationRequest());

        result.IsFailure.ShouldBeTrue();
        // The catch block maps a GeoCodingException specifically to LocationNotFound, not the generic DefaultError.
        result.Error.ShouldBe(GetCurrentLocationErrors.LocationNotFound);
    }

    [Fact]
    public async Task UnexpectedException_ReturnsDefaultError()
    {
        var preferencesHandler = A.Fake<IHandler<GetPreferencesRequest, GetPreferencesResponse>>();
        A.CallTo(() =>
                preferencesHandler.HandleAsync(A<GetPreferencesRequest>._, A<CancellationToken>._)
            )
            .Returns(Result<GetPreferencesResponse>.Success(PreferencesWithLocation(2459115)));

        var geoCodingApi = A.Fake<IGeoCodingApi>();
        A.CallTo(() => geoCodingApi.GetByIdAsync(2459115, A<CancellationToken>._))
            .Throws(new InvalidOperationException("boom"));

        var logger = A.Fake<ILogger<GetCurrentLocationHandler>>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new GetCurrentLocationHandler(
            geoCodingApi,
            preferencesHandler,
            logger,
            cache
        );

        var result = await handler.HandleAsync(new GetCurrentLocationRequest());

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GetCurrentLocationErrors.DefaultError);
    }

    [Fact]
    public async Task SuccessfulLookup_ReturnsMappedResponseAndCachesPerLocationId()
    {
        var preferencesHandler = A.Fake<IHandler<GetPreferencesRequest, GetPreferencesResponse>>();
        A.CallTo(() =>
                preferencesHandler.HandleAsync(A<GetPreferencesRequest>._, A<CancellationToken>._)
            )
            .Returns(Result<GetPreferencesResponse>.Success(PreferencesWithLocation(2459115)));

        var geoCodingApi = A.Fake<IGeoCodingApi>();
        A.CallTo(() => geoCodingApi.GetByIdAsync(2459115, A<CancellationToken>._))
            .Returns(
                Task.FromResult<GeoCodingLocation?>(
                    new GeoCodingLocation
                    {
                        Id = 2459115,
                        Name = "New York",
                        Latitude = 40.7128,
                        Longitude = -74.0060,
                    }
                )
            );

        var logger = A.Fake<ILogger<GetCurrentLocationHandler>>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new GetCurrentLocationHandler(
            geoCodingApi,
            preferencesHandler,
            logger,
            cache
        );

        var firstResult = await handler.HandleAsync(new GetCurrentLocationRequest());
        firstResult.IsSuccess.ShouldBeTrue();
        firstResult.Value.ShouldNotBeNull().Name.ShouldBe("New York");

        var secondResult = await handler.HandleAsync(new GetCurrentLocationRequest());
        secondResult.IsSuccess.ShouldBeTrue();

        A.CallTo(() => geoCodingApi.GetByIdAsync(2459115, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
