using FakeItEasy;
using Kelvin.Protocol;
using Kelvin.Server.Application;
using Kelvin.Server.Features.Hmi;
using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Panels;

public class HmiMessageTests
{
    [Fact]
    public void AllServerStatePayloads_RoundTripThroughSimulator()
    {
        var id = new Guid("03020100-0504-0706-0809-0a0b0c0d0e0f");
        var thermostat = new Models.Thermostat
        {
            Mode = RunMode.Heating,
            FanEnabled = true,
            HysteresisC = 0.5f,
            HeatingLockoutC = null,
            CoolingLockoutC = 25f,
        };
        thermostat.SetPoints.Add(new SetPoint { Type = RunType.Heating, TargetTemperatureC = 22f });
        thermostat.SetPoints.Add(
            new SetPoint
            {
                Type = RunType.Cooling,
                TargetTemperatureC = 25f,
                DeletedAt = DateTimeOffset.UtcNow,
            }
        );
        thermostat.Schedules.Add(
            new Schedule
            {
                Id = id,
                Type = RunType.Heating,
                StartTime = new TimeOnly(6, 0),
                EndTime = new TimeOnly(7, 0),
                TargetTemperatureC = 22f,
            }
        );
        var state = Simulator.HmiProtocol.DecodeThermostatState(
            HmiThermostatStateEncoder.Encode(thermostat)
        );
        state.ShouldNotBeNull();
        state.Mode.ShouldBe(Simulator.HmiRunMode.Heating);
        state.FanEnabled.ShouldBeTrue();
        state.HysteresisC.ShouldBe(0.5f);
        state.HeatingLockoutC.ShouldBeNull();
        state.CoolingLockoutC.ShouldBe(25f);
        state.SetPoints.ShouldHaveSingleItem().TargetTemperatureC.ShouldBe(22f);
        state.Schedules.ShouldHaveSingleItem().Id.ShouldBe(id);
        state.Schedules[0].Start.ShouldBe(new TimeOnly(6, 0));
        state.Schedules[0].End.ShouldBe(new TimeOnly(7, 0));
        thermostat.CoolingLockoutC = null;
        Convert
            .ToHexString(HmiThermostatStateEncoder.Encode(thermostat))
            .ShouldBe(
                "A80001010202F503F9380004F605F60681A2010002F94D800781A50150000102030405060708090A0B0C0D0E0F020003190168041901A405F94D80"
            );
        var control = Simulator.HmiProtocol.DecodeControlState(
            HmiControlStateEncoder.Encode(
                new ControlStateChange
                {
                    State = ControlState.Heating,
                    EnvironmentTemperatureC = 21.5f,
                    TargetTemperatureC = 22f,
                }
            )
        );
        control.ShouldNotBeNull();
        control.State.ShouldBe(Simulator.HmiControlState.Heating);
        control.EnvironmentTemperatureC.ShouldBe(21.5f);
        control.TargetTemperatureC.ShouldBe(22f);
        control.HumidityPercentage.ShouldBeNull();
        var preferences = Simulator.HmiProtocol.DecodePreferences(
            HmiPreferencesEncoder.Encode(
                new Models.Preferences
                {
                    TemperatureUnit = TemperatureUnit.Fahrenheit,
                    TimeFormat = TimeFormat.Hour12,
                }
            )
        );
        preferences.ShouldNotBeNull();
        preferences.TemperatureUnit.ShouldBe(1u);
        preferences.TimeFormat.ShouldBe(1u);
    }

    [Fact]
    public void SnapshotBeyondPanelCapacity_IsRejected()
    {
        var thermostat = new Models.Thermostat();
        for (var index = 0; index < 33; index++)
            thermostat.Schedules.Add(new Schedule());
        Should.Throw<ArgumentOutOfRangeException>(() =>
            HmiThermostatStateEncoder.Encode(thermostat)
        );
    }

    [Theory]
    [InlineData(2)]
    [InlineData(0x10)]
    [InlineData(0x11)]
    [InlineData(0x12)]
    [InlineData(0x13)]
    [InlineData(0x14)]
    [InlineData(0x15)]
    [InlineData(0x16)]
    public async Task EverySimulatorUplink_IsDecodedByRealServerHandler(int messageType)
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var id = Guid.NewGuid();
        var thermostat = new Models.Thermostat { Mode = RunMode.Heating };
        thermostat.Schedules.Add(
            new Schedule
            {
                Id = id,
                Type = RunType.Heating,
                StartTime = new(6, 0),
                EndTime = new(7, 0),
                TargetTemperatureC = 22f,
            }
        );
        context.Thermostats.Add(thermostat);
        await context.SaveChangesAsync();
        var update = A.Fake<IHandler<UpdateThermostatRequest>>();
        var settings = A.Fake<IHandler<UpdateThermostatSettingsRequest>>();
        var sensor = A.Fake<IHandler<SaveSensorPacketRequest>>();
        var snapshot = A.Fake<IHandler<SendHmiStateRequest>>();
        UpdateThermostatRequest? modeRequest = null;
        UpdateThermostatSettingsRequest? settingsRequest = null;
        SaveSensorPacketRequest? sensorRequest = null;
        A.CallTo(() => update.HandleAsync(A<UpdateThermostatRequest>._, A<CancellationToken>._))
            .Invokes(
                (UpdateThermostatRequest request, CancellationToken _) => modeRequest = request
            )
            .Returns(Result.Success());
        A.CallTo(() =>
                settings.HandleAsync(A<UpdateThermostatSettingsRequest>._, A<CancellationToken>._)
            )
            .Invokes(
                (UpdateThermostatSettingsRequest request, CancellationToken _) =>
                    settingsRequest = request
            )
            .Returns(Result.Success());
        A.CallTo(() => sensor.HandleAsync(A<SaveSensorPacketRequest>._, A<CancellationToken>._))
            .Invokes(
                (SaveSensorPacketRequest request, CancellationToken _) => sensorRequest = request
            )
            .Returns(Result.Success());
        A.CallTo(() => snapshot.HandleAsync(A<SendHmiStateRequest>._, A<CancellationToken>._))
            .Returns(Result.Success());
        var type = (HmiMessageType)messageType;
        var body = type switch
        {
            HmiMessageType.SensorReading => Simulator.HmiProtocol.EncodeSensorReading(
                21.5f,
                50f,
                75f
            ),
            HmiMessageType.SetMode => Simulator.HmiProtocol.EncodeSetMode(
                Simulator.HmiRunMode.Cooling
            ),
            HmiMessageType.SetFanEnabled => Simulator.HmiProtocol.EncodeSetFanEnabled(true),
            HmiMessageType.SetSetPoint => Simulator.HmiProtocol.EncodeSetSetPoint(
                Simulator.HmiRunType.Heating,
                23f
            ),
            HmiMessageType.UpsertSchedule => Simulator.HmiProtocol.EncodeUpsertSchedule(
                id,
                Simulator.HmiRunType.Cooling,
                new(8, 0),
                new(9, 0),
                24f
            ),
            HmiMessageType.RemoveSchedule => Simulator.HmiProtocol.EncodeRemoveSchedule(id),
            HmiMessageType.SetForecastLockouts => Simulator.HmiProtocol.EncodeSetForecastLockouts(
                null,
                26f
            ),
            _ => Simulator.HmiProtocol.EncodeRequestState(),
        };
        var handler = new ReceiveHmiCommandHandler(context, update, settings, sensor, snapshot);
        var frame = Simulator.HmiProtocol.Envelope((Simulator.HmiMessageType)messageType, body);
        var result = await handler.HandleAsync(
            new ReceiveHmiCommandRequest("AA:BB:CC:DD:EE:FF", frame)
        );
        result.IsSuccess.ShouldBeTrue();
        if (type == HmiMessageType.SensorReading)
        {
            sensorRequest.ShouldNotBeNull();
            sensorRequest.SensorPacket.TemperatureC.ShouldBe(21.5f);
            sensorRequest.SensorPacket.BatteryLevelPercentage.ShouldBe(75f);
        }
        else if (type == HmiMessageType.SetMode)
            modeRequest.ShouldNotBeNull().Mode.ShouldBe(RunMode.Cooling);
        else if (type == HmiMessageType.SetFanEnabled)
            modeRequest.ShouldNotBeNull().FanEnabled.ShouldBeTrue();
        else if (type == HmiMessageType.SetSetPoint)
            settingsRequest.ShouldNotBeNull().SetPoints.Single().TargetTemperatureC.ShouldBe(23f);
        else if (type == HmiMessageType.UpsertSchedule)
        {
            var schedule = settingsRequest.ShouldNotBeNull().Schedules.Single();
            schedule.Id.ShouldBe(id);
            schedule.StartTime.ShouldBe(new TimeOnly(8, 0));
            schedule.EndTime.ShouldBe(new TimeOnly(9, 0));
            schedule.TargetTemperatureC.ShouldBe(24f);
        }
        else if (type == HmiMessageType.RemoveSchedule)
            settingsRequest.ShouldNotBeNull().Schedules.ShouldBeEmpty();
        else if (type == HmiMessageType.SetForecastLockouts)
        {
            settingsRequest.ShouldNotBeNull().HeatingLockoutC.ShouldBeNull();
            settingsRequest.CoolingLockoutC.ShouldBe(26f);
        }
        else
            A.CallTo(() => snapshot.HandleAsync(A<SendHmiStateRequest>._, A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(0x10, "A200010105")]
    [InlineData(0x11, "A200010101")]
    [InlineData(0x12, "A30001010202F94D80")]
    [InlineData(0x13, "A6000101F60200031905A0040005F94D80")]
    [InlineData(0x14, "A20001014100")]
    [InlineData(0x15, "A3000101F97C0002F6")]
    [InlineData(0x16, "A10002")]
    [InlineData(0x10, "A10001")]
    [InlineData(0x10, "A20001010100")]
    public async Task MalformedCommands_AreRejectedBeforeRegisteringOrDispatching(
        int messageType,
        string hex
    )
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        context.Thermostats.Add(new Models.Thermostat());
        await context.SaveChangesAsync();
        var update = A.Fake<IHandler<UpdateThermostatRequest>>();
        var settings = A.Fake<IHandler<UpdateThermostatSettingsRequest>>();
        var sensor = A.Fake<IHandler<SaveSensorPacketRequest>>();
        var snapshot = A.Fake<IHandler<SendHmiStateRequest>>();
        var handler = new ReceiveHmiCommandHandler(context, update, settings, sensor, snapshot);
        var frame = HmiEnvelope
            .Encode((HmiMessageType)messageType, Convert.FromHexString(hex))
            .Single();
        var result = await handler.HandleAsync(
            new ReceiveHmiCommandRequest("AA:BB:CC:DD:EE:FF", frame)
        );
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ReceiveHmiCommandErrors.InvalidPayload);
        context.Hmis.ShouldBeEmpty();
        A.CallTo(() => update.HandleAsync(A<UpdateThermostatRequest>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() =>
                settings.HandleAsync(A<UpdateThermostatSettingsRequest>._, A<CancellationToken>._)
            )
            .MustNotHaveHappened();
        A.CallTo(() => sensor.HandleAsync(A<SaveSensorPacketRequest>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => snapshot.HandleAsync(A<SendHmiStateRequest>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }
}
