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
    [Theory]
    [InlineData(RunMode.Heating, RunType.Heating, 26f, true)]
    [InlineData(RunMode.Cooling, RunType.Cooling, 18f, true)]
    [InlineData(RunMode.Automatic, RunType.Heating, 26f, false)]
    [InlineData(RunMode.Automatic, RunType.Cooling, 18f, false)]
    public async Task SetPointCommand_ValidatesOnlyAvailableModes(
        RunMode mode,
        RunType type,
        float target,
        bool expectedSuccess
    )
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var thermostat = new Models.Thermostat { Mode = mode };
        thermostat.SetPoints.Add(new SetPoint { Type = RunType.Heating, TargetTemperatureC = 20 });
        thermostat.SetPoints.Add(new SetPoint { Type = RunType.Cooling, TargetTemperatureC = 24 });
        var inactiveSchedule = new Schedule
        {
            Type = type == RunType.Heating ? RunType.Cooling : RunType.Heating,
            StartTime = new TimeOnly(6, 0),
            EndTime = new TimeOnly(7, 0),
            TargetTemperatureC = type == RunType.Heating ? 24 : 20,
        };
        thermostat.Schedules.Add(inactiveSchedule);
        context.Thermostats.Add(thermostat);
        await context.SaveChangesAsync();
        var dispatcher = A.Fake<IDispatcher>();
        var validator = new ValidateThermostatSafetyHandler();
        A.CallTo(() =>
                dispatcher.DispatchAsync(
                    A<ValidateThermostatSafetyRequest>._,
                    A<CancellationToken>._
                )
            )
            .ReturnsLazily(
                (ValidateThermostatSafetyRequest request, CancellationToken ct) =>
                    validator.HandleAsync(request, ct)
            );
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()
        );
        var handler = new ReceiveHmiCommandHandler(
            context,
            A.Fake<IHandler<UpdateThermostatRequest>>(),
            new UpdateThermostatSettingsHandler(context, cache, dispatcher),
            A.Fake<IHandler<SaveSensorPacketRequest>>(),
            A.Fake<IHandler<SendHmiStateRequest>>(),
            A.Fake<IHandler<Kelvin.Server.Features.Notifications.DismissNotificationRequest>>()
        );
        var frame = Simulator.HmiProtocol.Envelope(
            Simulator.HmiMessageType.SetSetPoint,
            Simulator.HmiProtocol.EncodeSetSetPoint((Simulator.HmiRunType)type, target)
        );

        var result = await handler.HandleAsync(
            new ReceiveHmiCommandRequest("AA:BB:CC:DD:EE:FF", frame)
        );

        result.IsSuccess.ShouldBe(expectedSuccess);
        if (expectedSuccess)
        {
            context
                .SetPoints.Single(value => value.Type == type && value.DeletedAt == null)
                .TargetTemperatureC.ShouldBe(target);
            context
                .SetPoints.Single(value => value.Type != type && value.DeletedAt == null)
                .TargetTemperatureC.ShouldBe(type == RunType.Heating ? 24f : 20f);
            context
                .Schedules.Single(value => value.Id == inactiveSchedule.Id)
                .DeletedAt.ShouldBeNull();
        }
        else
            result.Error.ShouldBe(ValidateThermostatSafetyErrors.UnsafeTargetOverlap);
    }

    [Theory]
    [InlineData(RunMode.Automatic)]
    [InlineData(RunMode.Heating)]
    [InlineData(RunMode.Cooling)]
    [InlineData(RunMode.Off)]
    [InlineData(RunMode.Disabled)]
    public async Task ForecastLockoutCommand_OnlyChangesLockoutsInActiveMode(RunMode mode)
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var thermostat = new Models.Thermostat
        {
            Mode = mode,
            HeatingLockoutC = 10,
            CoolingLockoutC = 30,
        };
        context.Thermostats.Add(thermostat);
        await context.SaveChangesAsync();
        var dispatcher = A.Fake<IDispatcher>();
        A.CallTo(() =>
                dispatcher.DispatchAsync(
                    A<ValidateThermostatSafetyRequest>._,
                    A<CancellationToken>._
                )
            )
            .Returns(Result.Success());
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()
        );
        var handler = new ReceiveHmiCommandHandler(
            context,
            A.Fake<IHandler<UpdateThermostatRequest>>(),
            new UpdateThermostatSettingsHandler(context, cache, dispatcher),
            A.Fake<IHandler<SaveSensorPacketRequest>>(),
            A.Fake<IHandler<SendHmiStateRequest>>(),
            A.Fake<IHandler<Kelvin.Server.Features.Notifications.DismissNotificationRequest>>()
        );
        var frame = Simulator.HmiProtocol.Envelope(
            Simulator.HmiMessageType.SetForecastLockouts,
            Simulator.HmiProtocol.EncodeSetForecastLockouts(null, 26)
        );
        (
            await handler.HandleAsync(new ReceiveHmiCommandRequest("AA:BB:CC:DD:EE:FF", frame))
        ).IsSuccess.ShouldBeTrue();
        thermostat.HeatingLockoutC.ShouldBe(
            mode is RunMode.Heating or RunMode.Automatic ? null : 10f
        );
        thermostat.CoolingLockoutC.ShouldBe(
            mode is RunMode.Cooling or RunMode.Automatic ? 26f : 30f
        );
    }

    [Theory]
    [InlineData(RunMode.Heating, 0x13, false)]
    [InlineData(RunMode.Cooling, 0x13, false)]
    [InlineData(RunMode.Heating, 0x14, false)]
    [InlineData(RunMode.Cooling, 0x14, false)]
    [InlineData(RunMode.Heating, 0x15, false)]
    [InlineData(RunMode.Cooling, 0x15, false)]
    [InlineData(RunMode.Heating, 0x13, true)]
    [InlineData(RunMode.Cooling, 0x13, true)]
    public async Task SettingsCommands_IgnoreAndPreserveInactiveSettings(
        RunMode mode,
        int command,
        bool overlapping
    )
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var type = mode == RunMode.Heating ? RunType.Heating : RunType.Cooling;
        var thermostat = new Models.Thermostat
        {
            Mode = mode,
            HeatingLockoutC = 30,
            CoolingLockoutC = 10,
        };
        thermostat.SetPoints.Add(new SetPoint { Type = RunType.Heating, TargetTemperatureC = 26 });
        thermostat.SetPoints.Add(new SetPoint { Type = RunType.Cooling, TargetTemperatureC = 18 });
        var active = new Schedule
        {
            Type = type,
            StartTime = new TimeOnly(6, 0),
            EndTime = new TimeOnly(7, 0),
            TargetTemperatureC = type == RunType.Heating ? 26 : 18,
        };
        var inactive = new Schedule
        {
            Type = type == RunType.Heating ? RunType.Cooling : RunType.Heating,
            StartTime = new TimeOnly(6, 0),
            EndTime = new TimeOnly(7, 0),
            TargetTemperatureC = type == RunType.Heating ? 18 : 26,
        };
        thermostat.Schedules.Add(active);
        thermostat.Schedules.Add(inactive);
        context.Thermostats.Add(thermostat);
        await context.SaveChangesAsync();
        var dispatcher = A.Fake<IDispatcher>();
        var validator = new ValidateThermostatSafetyHandler();
        A.CallTo(() =>
                dispatcher.DispatchAsync(
                    A<ValidateThermostatSafetyRequest>._,
                    A<CancellationToken>._
                )
            )
            .ReturnsLazily(
                (ValidateThermostatSafetyRequest request, CancellationToken ct) =>
                    validator.HandleAsync(request, ct)
            );
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()
        );
        var handler = new ReceiveHmiCommandHandler(
            context,
            A.Fake<IHandler<UpdateThermostatRequest>>(),
            new UpdateThermostatSettingsHandler(context, cache, dispatcher),
            A.Fake<IHandler<SaveSensorPacketRequest>>(),
            A.Fake<IHandler<SendHmiStateRequest>>(),
            A.Fake<IHandler<Kelvin.Server.Features.Notifications.DismissNotificationRequest>>()
        );
        var body = command switch
        {
            0x13 => Simulator.HmiProtocol.EncodeUpsertSchedule(
                overlapping ? null : active.Id,
                (Simulator.HmiRunType)type,
                overlapping ? new TimeOnly(6, 30) : new TimeOnly(8, 0),
                overlapping ? new TimeOnly(7, 30) : new TimeOnly(9, 0),
                type == RunType.Heating ? 27 : 17
            ),
            0x14 => Simulator.HmiProtocol.EncodeRemoveSchedule(active.Id),
            _ => Simulator.HmiProtocol.EncodeSetForecastLockouts(32, 8),
        };
        var frame = Simulator.HmiProtocol.Envelope((Simulator.HmiMessageType)command, body);

        var result = await handler.HandleAsync(
            new ReceiveHmiCommandRequest("AA:BB:CC:DD:EE:FF", frame)
        );

        if (overlapping)
        {
            result.IsFailure.ShouldBeTrue();
            result.Error.ShouldBe(ValidateThermostatSafetyErrors.OverlappingSchedulesSameType);
            return;
        }
        result.IsSuccess.ShouldBeTrue(result.Error.Message);
        var activeSchedules = context
            .Schedules.Where(value => value.Type == type && value.DeletedAt == null)
            .ToList();
        activeSchedules.Count.ShouldBe(command == 0x14 ? 0 : 1);
        if (command == 0x13)
            activeSchedules.Single().TargetTemperatureC.ShouldBe(type == RunType.Heating ? 27 : 17);
        context.Schedules.Single(value => value.Id == inactive.Id).DeletedAt.ShouldBeNull();
        context
            .SetPoints.Single(value => value.Type != type && value.DeletedAt == null)
            .TargetTemperatureC.ShouldBe(type == RunType.Heating ? 18 : 26);
        thermostat.HeatingLockoutC.ShouldBe(command == 0x15 && type == RunType.Heating ? 32 : 30);
        thermostat.CoolingLockoutC.ShouldBe(command == 0x15 && type == RunType.Cooling ? 8 : 10);
    }

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
        var thermostat = new Models.Thermostat
        {
            Mode =
                messageType == (int)HmiMessageType.UpsertSchedule
                    ? RunMode.Automatic
                    : RunMode.Heating,
        };
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
        var handler = new ReceiveHmiCommandHandler(
            context,
            update,
            settings,
            sensor,
            snapshot,
            A.Fake<IHandler<Kelvin.Server.Features.Notifications.DismissNotificationRequest>>()
        );
        var frame = Simulator.HmiProtocol.Envelope((Simulator.HmiMessageType)messageType, body);
        var result = await handler.HandleAsync(
            new ReceiveHmiCommandRequest("AA:BB:CC:DD:EE:FF", frame)
        );
        result.IsSuccess.ShouldBeTrue();
        if (settingsRequest is not null)
            settingsRequest.TargetMode.ShouldBe(thermostat.Mode);
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
    [InlineData(7, "A20001014100")]
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
        var handler = new ReceiveHmiCommandHandler(
            context,
            update,
            settings,
            sensor,
            snapshot,
            A.Fake<IHandler<Kelvin.Server.Features.Notifications.DismissNotificationRequest>>()
        );
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
