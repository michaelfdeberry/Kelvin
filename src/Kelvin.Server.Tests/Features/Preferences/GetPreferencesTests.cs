using Kelvin.Server.Features.Preferences;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Preferences;

/// <summary>
/// Tests for <see cref="GetPreferencesHandler"/>, which auto-creates a default row the first time it is read.
/// </summary>
public class GetPreferencesTests
{
    [Fact]
    public async Task NoPreferencesExist_CreatesDefaultAndPersistsIt()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();

        var result = await new GetPreferencesHandler(context).HandleAsync(
            new GetPreferencesRequest()
        );

        var preferences = result.Value.ShouldNotBeNull();
        preferences.TemperatureUnit.ShouldBe(TemperatureUnit.Celsius);
        preferences.TimeFormat.ShouldBe(TimeFormat.Hour24);
        preferences.LocationId.ShouldBeNull();
        preferences.LocationName.ShouldBeNull();

        await using var readContext = harness.CreateContext();
        readContext.Preferences.Count().ShouldBe(1);
    }

    [Fact]
    public async Task ExistingPreferences_ReturnsStoredValues_AndDoesNotCreateASecondRow()
    {
        using var harness = new KelvinContextHarness();
        await using (var context = harness.CreateContext())
        {
            context.Preferences.Add(
                new Models.Preferences
                {
                    TemperatureUnit = TemperatureUnit.Fahrenheit,
                    TimeFormat = TimeFormat.Hour24,
                    LocationId = 2459115,
                    LocationName = "New York",
                }
            );
            await context.SaveChangesAsync();
        }

        await using var readContext = harness.CreateContext();
        var result = await new GetPreferencesHandler(readContext).HandleAsync(
            new GetPreferencesRequest()
        );

        var preferences = result.Value.ShouldNotBeNull();
        preferences.TemperatureUnit.ShouldBe(TemperatureUnit.Fahrenheit);
        preferences.TimeFormat.ShouldBe(TimeFormat.Hour24);
        preferences.LocationId.ShouldBe(2459115);
        preferences.LocationName.ShouldBe("New York");

        await using var verifyContext = harness.CreateContext();
        verifyContext.Preferences.Count().ShouldBe(1);
    }
}
