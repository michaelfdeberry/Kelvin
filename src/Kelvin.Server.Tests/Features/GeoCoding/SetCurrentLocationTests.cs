using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Features.GeoCoding;
using Kelvin.Server.Features.Weather;
using Kelvin.Server.Integration.GeoCoding;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.GeoCoding;

public class SetCurrentLocationTests
{
    [Fact]
    public async Task InvalidLocationId_IsRejected()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();

        var geoCodingApi = A.Fake<IGeoCodingApi>();
        var logger = A.Fake<ILogger<SetCurrentLocationHandler>>();
        var handler = new SetCurrentLocationHandler(context, geoCodingApi, A.Fake<IDispatcher>(), logger);

        var result = await handler.HandleAsync(new SetCurrentLocationRequest(0));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SetCurrentLocationErrors.InvalidLocation);
    }

    [Fact]
    public async Task UnknownLocation_IsReportedAsNotFound()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();

        var geoCodingApi = A.Fake<IGeoCodingApi>();
        A.CallTo(() => geoCodingApi.GetByIdAsync(101, A<CancellationToken>._))
            .Returns(Task.FromResult<GeoCodingLocation?>(null));

        var logger = A.Fake<ILogger<SetCurrentLocationHandler>>();
        var handler = new SetCurrentLocationHandler(context, geoCodingApi, A.Fake<IDispatcher>(), logger);

        var result = await handler.HandleAsync(new SetCurrentLocationRequest(101));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SetCurrentLocationErrors.LocationNotFound);
    }

    [Fact]
    public async Task SetsLocationIdAndNameInPreferences()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();

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

        var logger = A.Fake<ILogger<SetCurrentLocationHandler>>();
        var dispatcher = A.Fake<IDispatcher>();
        var handler = new SetCurrentLocationHandler(context, geoCodingApi, dispatcher, logger);

        var result = await handler.HandleAsync(new SetCurrentLocationRequest(2459115));

        result.IsSuccess.ShouldBeTrue();

        var preferences = context.Preferences.Single();
        preferences.LocationId.ShouldBe(2459115);
        preferences.LocationName.ShouldBe("New York");
        A.CallTo(() =>
                dispatcher.PublishAsync(
                    A<PreferencesChangedEvent>.That.Matches(e => e.Preferences == preferences),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PushesTheForecastForTheNewLocation()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();

        var geoCodingApi = A.Fake<IGeoCodingApi>();
        A.CallTo(() => geoCodingApi.GetByIdAsync(2459115, A<CancellationToken>._))
            .Returns(
                Task.FromResult<GeoCodingLocation?>(
                    new GeoCodingLocation { Id = 2459115, Name = "New York" }
                )
            );

        var forecast = new GetWeatherForecastResponse(0, 0, "UTC", DateTimeOffset.UtcNow, null, []);
        var dispatcher = A.Fake<IDispatcher>();
        A.CallTo(() =>
                dispatcher.DispatchAsync<GetWeatherForecastRequest, GetWeatherForecastResponse>(
                    A<GetWeatherForecastRequest>._,
                    A<CancellationToken>._
                )
            )
            .Returns(Result<GetWeatherForecastResponse>.Success(forecast));
        var handler = new SetCurrentLocationHandler(
            context,
            geoCodingApi,
            dispatcher,
            A.Fake<ILogger<SetCurrentLocationHandler>>()
        );

        var result = await handler.HandleAsync(new SetCurrentLocationRequest(2459115));

        result.IsSuccess.ShouldBeTrue();
        A.CallTo(() =>
                dispatcher.PublishAsync(
                    A<WeatherForecastChangedEvent>.That.Matches(e => e.Forecast == forecast),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task AForecastThatCannotBeRetrieved_DoesNotFailTheRequest()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();

        var geoCodingApi = A.Fake<IGeoCodingApi>();
        A.CallTo(() => geoCodingApi.GetByIdAsync(2459115, A<CancellationToken>._))
            .Returns(
                Task.FromResult<GeoCodingLocation?>(
                    new GeoCodingLocation { Id = 2459115, Name = "New York" }
                )
            );

        var dispatcher = A.Fake<IDispatcher>();
        A.CallTo(() =>
                dispatcher.DispatchAsync<GetWeatherForecastRequest, GetWeatherForecastResponse>(
                    A<GetWeatherForecastRequest>._,
                    A<CancellationToken>._
                )
            )
            .Returns(
                Result<GetWeatherForecastResponse>.Failure(
                    GetWeatherForecastErrors.ForecastNotFound
                )
            );
        var handler = new SetCurrentLocationHandler(
            context,
            geoCodingApi,
            dispatcher,
            A.Fake<ILogger<SetCurrentLocationHandler>>()
        );

        var result = await handler.HandleAsync(new SetCurrentLocationRequest(2459115));

        result.IsSuccess.ShouldBeTrue();
        context.Preferences.Single().LocationId.ShouldBe(2459115);
        A.CallTo(() =>
                dispatcher.PublishAsync(
                    A<WeatherForecastChangedEvent>._,
                    A<CancellationToken>._
                )
            )
            .MustNotHaveHappened();
    }
}
