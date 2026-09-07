using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Sensors;

public class CleanupSensorPacketsTests
{
    [Fact]
    public async Task DeletesOnlyPacketsOlderThanThirtyDays()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        context.SensorPackets.AddRange(
            new SensorPacket { MacAddress = "old", CreatedAt = DateTimeOffset.UtcNow.AddDays(-31) },
            new SensorPacket { MacAddress = "recent", CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) }
        );
        await context.SaveChangesAsync();
        await context.SensorPackets.Where(packet => packet.MacAddress == "old").ExecuteUpdateAsync(update =>
            update.SetProperty(packet => packet.CreatedAt, DateTimeOffset.UtcNow.AddDays(-31)));
        await context.SensorPackets.Where(packet => packet.MacAddress == "recent").ExecuteUpdateAsync(update =>
            update.SetProperty(packet => packet.CreatedAt, DateTimeOffset.UtcNow.AddDays(-1)));

        var result = await new CleanupSensorPacketsHandler(context).HandleAsync(new CleanupSensorPacketsRequest());

        result.IsSuccess.ShouldBeTrue();
        await using var readContext = harness.CreateContext();
        readContext.SensorPackets.Select(packet => packet.MacAddress).ShouldBe(["recent"]);
    }
}