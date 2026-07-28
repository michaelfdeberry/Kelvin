using FakeItEasy;
using Kelvin.Server.Features.GeoCoding;
using Kelvin.Server.Integration.GeoCoding;
using Kelvin.Server.Models;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.GeoCoding;

public class SearchLocationsTests
{
    [Fact]
    public async Task EmptyQuery_IsRejected()
    {
        var geoCodingApi = A.Fake<IGeoCodingApi>();
        var logger = A.Fake<ILogger<SearchLocationsHandler>>();
        var handler = new SearchLocationsHandler(geoCodingApi, logger);

        var result = await handler.HandleAsync(new SearchLocationsRequest("   "));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SearchLocationsErrors.InvalidQuery);
    }

    [Fact]
    public async Task SearchResult_MapsToApiResponseShape()
    {
        var geoCodingApi = A.Fake<IGeoCodingApi>();
        A.CallTo(() => geoCodingApi.SearchAsync("new", 20, A<CancellationToken>._))
            .Returns(
                Task.FromResult<IReadOnlyList<GeoCodingLocation>>([
                    new GeoCodingLocation
                    {
                        Id = 1,
                        Name = "New York",
                        Latitude = 40.7128,
                        Longitude = -74.0060,
                        Country = "United States",
                        Admin1 = "New York",
                    },
                    new GeoCodingLocation
                    {
                        Id = 2,
                        Name = "Newark",
                        Latitude = 40.7357,
                        Longitude = -74.1724,
                        Country = "United States",
                        Admin1 = "New Jersey",
                    },
                ])
            );

        var logger = A.Fake<ILogger<SearchLocationsHandler>>();
        var handler = new SearchLocationsHandler(geoCodingApi, logger);

        var result = await handler.HandleAsync(new SearchLocationsRequest("new", 30));

        result.IsSuccess.ShouldBeTrue();

        var response = result.Value.ShouldNotBeNull();
        response.Locations.Count.ShouldBe(2);
        response.Locations[0].Id.ShouldBe(1);
        response.Locations[0].Name.ShouldBe("New York");
        response.Locations[1].Id.ShouldBe(2);
        response.Locations[1].Admin1.ShouldBe("New Jersey");
    }
}
