using Kelvin.Server.Features.Devices;
using Kelvin.Server.Hubs;
using Kelvin.Server.Models;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Hubs;

public class RealtimePayloadsTests
{
    [Fact]
    public void ThermostatSettings_ContainsTheThermostatAndOnlyItsLiveSetPointsAndSchedules()
    {
        var live = new SetPoint { Type = RunType.Heating, TargetTemperatureC = 20f };
        var thermostat = new Models.Thermostat
        {
            Mode = RunMode.Automatic,
            SetPoints =
            [
                live,
                new SetPoint
                {
                    Type = RunType.Cooling,
                    TargetTemperatureC = 25f,
                    DeletedAt = DateTimeOffset.UtcNow,
                },
            ],
            Schedules =
            [
                new Schedule
                {
                    Type = RunType.Heating,
                    StartTime = new TimeOnly(6, 0),
                    EndTime = new TimeOnly(8, 0),
                    TargetTemperatureC = 21f,
                },
                new Schedule { Type = RunType.Heating, DeletedAt = DateTimeOffset.UtcNow },
            ],
        };

        var payload = ThermostatSettingsPayload.FromEntity(thermostat);

        payload.Thermostat.Mode.ShouldBe(RunMode.Automatic);
        payload.SetPoints.Select(s => s.Id).ShouldBe([live.Id]);
        payload.Schedules.Single().TargetTemperatureC.ShouldBe(21f);
    }

    [Fact]
    public void DeviceChanged_IncludesTheHumidifierConfigurationForAHumidifier()
    {
        var device = new HumidifierDevice
        {
            Name = "Humidifier",
            Configuration = new HumidifierConfiguration { StartMonth = 10, EndMonth = 3 },
        };

        var payload = DeviceChangedPayload.FromEntity(device);

        payload.Device.Id.ShouldBe(device.Id);
        payload.Device.Type.ShouldBe(DeviceType.Humidifier);
        payload.Humidifier.ShouldNotBeNull();
        payload.Humidifier.StartMonth.ShouldBe(10);
    }
}
