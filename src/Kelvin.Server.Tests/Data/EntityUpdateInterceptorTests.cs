using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Data;

/// <summary>
/// Tests for <see cref="Kelvin.Server.Data.EntityUpdateInterceptor"/>, exercised through a real
/// <see cref="Kelvin.Server.Data.KelvinContext"/> over in-memory SQLite with a <see cref="Microsoft.Extensions.Time.Testing.FakeTimeProvider"/>.
/// </summary>
public class EntityUpdateInterceptorTests
{
    [Fact]
    public async Task Adding_StampsCreatedAtAndUpdatedAtFromTheClock()
    {
        using var harness = new KelvinContextHarness();
        var now = harness.Time.GetUtcNow();

        await using (var context = harness.CreateContext())
        {
            context.Sensors.Add(new Sensor { MacAddress = "aa:bb:cc:dd:ee:ff" });
            await context.SaveChangesAsync();
        }

        await using var reader = harness.CreateContext();
        var sensor = await reader.Sensors.SingleAsync();
        sensor.CreatedAt.ShouldBe(now);
        sensor.UpdatedAt.ShouldBe(now);
        sensor.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Adding_OverwritesATimestampSuppliedByTheCaller()
    {
        using var harness = new KelvinContextHarness();
        var now = harness.Time.GetUtcNow();

        await using (var context = harness.CreateContext())
        {
            // Nothing outside the interceptor is allowed to decide when something happened.
            context.Sensors.Add(
                new Sensor
                {
                    MacAddress = "aa:bb:cc:dd:ee:ff",
                    CreatedAt = new DateTimeOffset(1999, 12, 31, 23, 59, 59, TimeSpan.Zero),
                }
            );
            await context.SaveChangesAsync();
        }

        await using var reader = harness.CreateContext();
        var sensor = await reader.Sensors.SingleAsync();
        sensor.CreatedAt.ShouldBe(now);
    }

    [Fact]
    public async Task Modifying_StampsUpdatedAtButLeavesCreatedAtAlone()
    {
        using var harness = new KelvinContextHarness();
        var createdAt = harness.Time.GetUtcNow();

        await using (var context = harness.CreateContext())
        {
            context.Sensors.Add(new Sensor { MacAddress = "aa:bb:cc:dd:ee:ff" });
            await context.SaveChangesAsync();
        }

        harness.Time.Advance(TimeSpan.FromMinutes(5));
        var updatedAt = harness.Time.GetUtcNow();

        await using (var context = harness.CreateContext())
        {
            var sensor = await context.Sensors.SingleAsync();
            sensor.Name = "Living Room";
            await context.SaveChangesAsync();
        }

        await using var reader = harness.CreateContext();
        var stored = await reader.Sensors.SingleAsync();
        stored.CreatedAt.ShouldBe(createdAt);
        stored.UpdatedAt.ShouldBe(updatedAt);
    }

    [Fact]
    public async Task Removing_SoftDeletesInsteadOfDeleting()
    {
        using var harness = new KelvinContextHarness();

        await using (var context = harness.CreateContext())
        {
            context.Sensors.Add(new Sensor { MacAddress = "aa:bb:cc:dd:ee:ff" });
            await context.SaveChangesAsync();
        }

        harness.Time.Advance(TimeSpan.FromHours(1));
        var deletedAt = harness.Time.GetUtcNow();

        await using (var context = harness.CreateContext())
        {
            var sensor = await context.Sensors.SingleAsync();
            context.Sensors.Remove(sensor);
            await context.SaveChangesAsync();
        }

        await using var reader = harness.CreateContext();
        var stored = await reader.Sensors.SingleAsync();
        stored.DeletedAt.ShouldBe(deletedAt);
    }
}
