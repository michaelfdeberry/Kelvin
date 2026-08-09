using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Thermostat;

/// <summary>
/// Tests for <see cref="GetSchedulesHandler"/>.
/// </summary>
public class GetSchedulesTests
{
    [Fact]
    public async Task NoSchedules_ReturnsEmptyCollection()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();

        var result = await new GetSchedulesHandler(context).HandleAsync(new GetSchedulesRequest());

        result.Value.ShouldNotBeNull().Schedules.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReturnsAllSchedulesMappedToDto()
    {
        using var harness = new KelvinContextHarness();
        Guid scheduleId;
        await using (var context = harness.CreateContext())
        {
            var thermostat = new Models.Thermostat { Mode = RunMode.Heating, FanEnabled = false };
            var schedule = new Schedule
            {
                Type = RunType.Heating,
                StartTime = new TimeOnly(6, 0),
                EndTime = new TimeOnly(22, 0),
                TargetTemperatureC = 21f,
            };
            thermostat.Schedules.Add(schedule);
            context.Thermostats.Add(thermostat);
            await context.SaveChangesAsync();
            scheduleId = schedule.Id;
        }

        await using var readContext = harness.CreateContext();
        var result = await new GetSchedulesHandler(readContext).HandleAsync(
            new GetSchedulesRequest()
        );

        var response = result.Value.ShouldNotBeNull();
        var dto = response.Schedules.ShouldHaveSingleItem();
        dto.Id.ShouldBe(scheduleId);
        dto.Type.ShouldBe(RunType.Heating);
        dto.StartTime.ShouldBe(new TimeOnly(6, 0));
        dto.EndTime.ShouldBe(new TimeOnly(22, 0));
        dto.TargetTemperatureC.ShouldBe(21f);
    }
}
