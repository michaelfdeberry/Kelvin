using Kelvin.Server.Features.Preferences;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Preferences;

public class UpdatePreferencesTests
{
    [Fact]
    public async Task UpdatesExistingPreferencesAndReturnsUpdatedValues()
    {
        using var harness = new KelvinContextHarness();
        await using (var seedContext = harness.CreateContext())
        {
            seedContext.Preferences.Add(
                new Models.Preferences
                {
                    TimeFormat = TimeFormat.Hour24,
                    TemperatureUnit = TemperatureUnit.Celsius,
                    LocationId = 1,
                    LocationName = "Old location",
                }
            );
            await seedContext.SaveChangesAsync();
        }

        await using var context = harness.CreateContext();
        var result = await new UpdatePreferencesHandler(context).HandleAsync(
            new UpdatePreferencesRequest(
                TimeFormat.Hour12,
                TemperatureUnit.Fahrenheit,
                2,
                "New location"
            )
        );

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(
            new UpdatePreferencesResponse(
                TimeFormat.Hour12,
                TemperatureUnit.Fahrenheit,
                2,
                "New location"
            )
        );

        await using var readContext = harness.CreateContext();
        var preferences = readContext.Preferences.Single();
        preferences.TimeFormat.ShouldBe(TimeFormat.Hour12);
        preferences.TemperatureUnit.ShouldBe(TemperatureUnit.Fahrenheit);
        preferences.LocationId.ShouldBe(2);
        preferences.LocationName.ShouldBe("New location");
    }
}
