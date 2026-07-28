using FakeItEasy;
using Kelvin.Server.Features.GeoCoding;
using Kelvin.Server.Integration.GeoCoding;
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
        var handler = new SetCurrentLocationHandler(context, geoCodingApi, logger);

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
        var handler = new SetCurrentLocationHandler(context, geoCodingApi, logger);

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
        var handler = new SetCurrentLocationHandler(context, geoCodingApi, logger);

        var result = await handler.HandleAsync(new SetCurrentLocationRequest(2459115));

        result.IsSuccess.ShouldBeTrue();

        var preferences = context.Preferences.Single();
        preferences.LocationId.ShouldBe(2459115);
        preferences.LocationName.ShouldBe("New York");
    }
}
