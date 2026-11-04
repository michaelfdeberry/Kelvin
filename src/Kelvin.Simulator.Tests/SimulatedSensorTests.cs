using Shouldly;
using Xunit;

namespace Kelvin.Simulator.Tests;

public class SimulatedSensorTests
{
    [Fact]
    public void Create_InitializesIdentityAndReadings()
    {
        var sensor = SimulatedSensor.Create(3, 21.5f);

        sensor.SensorNumber.ShouldBe(3);
        sensor.MacAddress.ShouldBe(new byte[] { 0x02, 0xAA, 0, 0, 0, 0x13 });
        sensor.Enabled.ShouldBeTrue();
        sensor.RoomOffsetC.ShouldBeInRange(-0.6f, 0.6f);
        sensor.TemperatureC.ShouldBe(21.5f + sensor.RoomOffsetC);
        sensor.HumidityPercentage.ShouldBeInRange(40f, 50f);
        sensor.CO2LevelPpm.ShouldBeInRange((ushort)700, (ushort)849);
        sensor.BatteryLevelPercentage.ShouldBe(100f);
    }

    [Fact]
    public void Step_FollowsAmbientWithoutChangingRoomOffset()
    {
        var sensor = SimulatedSensor.Create(0, 21.5f);
        var offset = sensor.RoomOffsetC;

        foreach (var ambient in new[] { 15f, 30f, 21.5f })
        {
            sensor.Step(ambient);
            sensor.TemperatureC.ShouldBe(ambient + offset);
            sensor.RoomOffsetC.ShouldBe(offset);
        }

        sensor.BatteryLevelPercentage.ShouldBeLessThan(100f);
    }

    [Theory]
    [InlineData(0f, 0, 0f)]
    [InlineData(100f, 65535, 0.00001f)]
    public void Step_ClampsReadingsAndNeverLetsBatteryGoNegative(
        float humidity,
        int co2,
        float battery
    )
    {
        var sensor = SimulatedSensor.Create(0, 21.5f);
        sensor.HumidityPercentage = humidity;
        sensor.CO2LevelPpm = (ushort)co2;
        sensor.BatteryLevelPercentage = battery;

        sensor.Step(20f);

        sensor.HumidityPercentage.ShouldBeInRange(20f, 70f);
        sensor.CO2LevelPpm.ShouldBeInRange((ushort)400, (ushort)2500);
        sensor.BatteryLevelPercentage.ShouldBe(0f);
    }

    [Fact]
    public void Fleet_AddAndRemove_PreserveRemainingIdentitiesAndDoNotReuseNumbers()
    {
        var fleet = new SensorFleet(2, 21.5f);
        var remaining = fleet.Sensors[1];

        fleet.RemoveSensor(0, out var removed).ShouldBeTrue();
        removed.ShouldNotBeNull();
        removed.SensorNumber.ShouldBe(0);
        fleet.Sensors[0].ShouldBeSameAs(remaining);
        var added = fleet.AddSensor(25f);

        fleet.Count.ShouldBe(2);
        added.SensorNumber.ShouldBe(2);
        added.MacAddress.SequenceEqual(removed.MacAddress).ShouldBeFalse();
        added.TemperatureC.ShouldBe(25f + added.RoomOffsetC);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Fleet_InvalidIndex_DoesNotMutateRoster(int index)
    {
        var fleet = new SensorFleet(2, 21.5f);

        fleet.RemoveSensor(index, out var removed).ShouldBeFalse();
        removed.ShouldBeNull();
        fleet.SetSensorEnabled(index, false, out var sensor).ShouldBeFalse();
        sensor.ShouldBeNull();
        fleet.Describe(index).ShouldBe("Invalid sensor index.");
        fleet.Count.ShouldBe(2);
        fleet.ActiveSensors.Count().ShouldBe(2);
    }

    [Fact]
    public void Fleet_EnableAndDisable_ControlActiveReadings()
    {
        var fleet = new SensorFleet(2, 21.5f);

        fleet.SetSensorEnabled(0, false, out var sensor).ShouldBeTrue();
        sensor.ShouldBeSameAs(fleet.Sensors[0]);
        fleet.ActiveSensors.ShouldBe(new[] { fleet.Sensors[1] });
        fleet.Count.ShouldBe(2);
        fleet.Describe(0).ShouldContain("offline");
        fleet.SetSensorEnabled(0, true, out _).ShouldBeTrue();
        fleet.ActiveSensors.Count().ShouldBe(2);
        fleet.SetAllSensorsEnabled(false).ShouldBe(2);
        fleet.ActiveSensors.ShouldBeEmpty();
        fleet.SetAllSensorsEnabled(true).ShouldBe(2);
        fleet.ActiveSensors.Count().ShouldBe(2);
    }

    [Fact]
    public void Fleet_StepAll_UpdatesOfflineSensorsToo()
    {
        var fleet = new SensorFleet(2, 21.5f);
        fleet.SetSensorEnabled(0, false, out _);

        fleet.StepAll(30f);

        foreach (var sensor in fleet.Sensors)
            sensor.TemperatureC.ShouldBe(30f + sensor.RoomOffsetC);
        fleet.Sensors[0].Enabled.ShouldBeFalse();
    }

    [Fact]
    public void EmptyFleet_CanBePopulated()
    {
        var fleet = new SensorFleet(0, 21.5f);

        fleet.Count.ShouldBe(0);
        fleet.ActiveSensors.ShouldBeEmpty();
        fleet.SetAllSensorsEnabled(false).ShouldBe(0);
        fleet.StepAll(30f);
        fleet.AddSensor(25f).SensorNumber.ShouldBe(0);
        fleet.Count.ShouldBe(1);
    }
}
