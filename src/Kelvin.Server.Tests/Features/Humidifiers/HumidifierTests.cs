using FakeItEasy;
using Kelvin.Protocol;
using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Control;
using Kelvin.Server.Features.Devices;
using Kelvin.Server.Features.Humidifiers;
using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Features.Weather;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Humidifiers;

public class HumidifierTests : IDisposable
{
    private const string MacAddress = "aa:bb:cc:dd:ee:ff";

    private readonly KelvinContextHarness _harness = new();
    private readonly IDispatcher _dispatcher = A.Fake<IDispatcher>();
    private readonly DeviceStateHandler _deviceStates;
    private readonly IHandler<GetControlStateRequest, GetControlStateResponse> _controlState =
        A.Fake<IHandler<GetControlStateRequest, GetControlStateResponse>>();
    private readonly IHandler<GetWeatherForecastRequest, GetWeatherForecastResponse> _forecast =
        A.Fake<IHandler<GetWeatherForecastRequest, GetWeatherForecastResponse>>();
    private readonly IHandler<GetLatestReadingsRequest, GetLatestReadingsResponse> _readings =
        A.Fake<IHandler<GetLatestReadingsRequest, GetLatestReadingsResponse>>();

    public HumidifierTests()
    {
        _deviceStates = new DeviceStateHandler(_dispatcher);

        // Harness clock is 2024-01-01 (in season), heating, -5C outside (target 40%), 20% inside.
        SetControl(ControlState.Enable, ControlState.Heating);
        SetOutdoorTemperature(-5f);
        SetIndoorHumidity(20f);
    }

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void DefaultTargets_AreStoredInCelsius()
    {
        var targets = new HumidifierConfiguration().Targets;

        targets.Select(t => t.OutdoorTemperatureC).ShouldBe([-29f, -23f, -18f, -12f, -7f, -1f, 4f]);
        targets.Select(t => t.HumidityPercentage).ShouldBe([15f, 20f, 25f, 30f, 35f, 40f, 45f]);
    }

    [Theory]
    [InlineData(-40f, 15f)]
    [InlineData(-29f, 15f)]
    [InlineData(-25f, 20f)]
    [InlineData(-15f, 30f)]
    [InlineData(-5f, 40f)]
    [InlineData(4f, 45f)]
    public void TargetHumidity_UsesTheColdestThresholdNotExceeded(float outdoorC, float expected)
    {
        new HumidifierConfiguration().TargetHumidityFor(outdoorC).ShouldBe(expected);
    }

    [Fact]
    public void TargetHumidity_WarmerThanTheLastThreshold_IsNull()
    {
        new HumidifierConfiguration().TargetHumidityFor(4.5f).ShouldBeNull();
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(9, false)]
    public void Season_WrapsTheYear(int month, bool expected)
    {
        new HumidifierConfiguration().IsInSeason(month).ShouldBe(expected);
    }

    [Fact]
    public void Configuration_RoundTripsThroughJson()
    {
        var device = new HumidifierDevice
        {
            Configuration = new HumidifierConfiguration { StartMonth = 11, Targets = [new(0, 30)] },
        };

        var configuration = device.Configuration;

        configuration.StartMonth.ShouldBe(11);
        configuration.EndMonth.ShouldBe(3);
        configuration.Targets.ShouldBe([new HumidityTarget(0, 30)]);
    }

    [Fact]
    public async Task BelowTarget_WhileHeating_EnablesTheHumidifier()
    {
        var result = await EvaluateAsync(await AddDeviceAsync(), relayOn: false);

        result.Enabled.ShouldBeTrue();
        result.TargetHumidityPercentage.ShouldBe(40f);
    }

    [Fact]
    public async Task OutdoorAboveTheLastThreshold_DisablesTheHumidifier()
    {
        SetOutdoorTemperature(10f);

        var result = await EvaluateAsync(await AddDeviceAsync(), relayOn: true);

        result.Enabled.ShouldBeFalse();
    }

    [Theory]
    [InlineData(30f, 41f, false)]
    [InlineData(38f, 41f, true)]
    [InlineData(30f, 39f, true)]
    public async Task SensorAtTargetWellAboveTheAverage_DisablesTheHumidifier(
        float average,
        float highest,
        bool expected
    )
    {
        SetIndoorHumidity(average, highest);

        var result = await EvaluateAsync(await AddDeviceAsync(), relayOn: true);

        result.Enabled.ShouldBe(expected);
    }

    [Theory]
    [InlineData(ControlState.Enable, ControlState.Dwell)]
    [InlineData(ControlState.Enable, ControlState.Cooling)]
    [InlineData(ControlState.Disable, ControlState.Heating)]
    public async Task NotHeatingUnderKelvinControl_DisablesTheHumidifier(
        ControlState control,
        ControlState call
    )
    {
        SetControl(control, call);

        var result = await EvaluateAsync(await AddDeviceAsync(), relayOn: true);

        result.Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task OutOfSeason_DisablesTheHumidifier()
    {
        _harness.Time.SetUtcNow(new DateTimeOffset(2024, 7, 1, 12, 0, 0, TimeSpan.Zero));

        var result = await EvaluateAsync(await AddDeviceAsync(), relayOn: false);

        result.Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task DisabledDevice_DisablesTheHumidifier()
    {
        var result = await EvaluateAsync(await AddDeviceAsync(enabled: false), relayOn: false);

        result.Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task MissingForecast_DisablesTheHumidifier()
    {
        A.CallTo(() =>
                _forecast.HandleAsync(A<GetWeatherForecastRequest>._, A<CancellationToken>._)
            )
            .Returns(
                Result<GetWeatherForecastResponse>.Failure(GetWeatherForecastErrors.NotCached)
            );

        var result = await EvaluateAsync(await AddDeviceAsync(), relayOn: false);

        result.Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task Evaluation_OnlyReadsTheCachedForecast()
    {
        await EvaluateAsync(await AddDeviceAsync(), relayOn: false);

        A.CallTo(() =>
                _forecast.HandleAsync(
                    A<GetWeatherForecastRequest>.That.Matches(r => !r.CachedOnly),
                    A<CancellationToken>._
                )
            )
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task NoHumidityReadings_DisablesTheHumidifier()
    {
        SetIndoorHumidity(0f);

        var result = await EvaluateAsync(await AddDeviceAsync(), relayOn: true);

        result.Enabled.ShouldBeFalse();
    }

    [Theory]
    [InlineData(39f, false, false)]
    [InlineData(39f, true, true)]
    [InlineData(37.5f, false, true)]
    [InlineData(40f, true, false)]
    public async Task Hysteresis_DependsOnTheCurrentRelayState(
        float humidity,
        bool relayOn,
        bool expected
    )
    {
        SetIndoorHumidity(humidity);

        var result = await EvaluateAsync(await AddDeviceAsync(), relayOn);

        result.Enabled.ShouldBe(expected);
    }

    [Fact]
    public async Task CheckIn_FromUnknownDevice_RegistersItAndRequestsAStatePush()
    {
        var result = await ReceiveAsync(Uplink(HumidifierMessageType.CheckIn, relayOn: false));

        result.IsSuccess.ShouldBeTrue();
        A.CallTo(() => _dispatcher.PublishAsync(A<DevicesChangedEvent>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        using var context = _harness.CreateContext();
        var device = await context.Devices.OfType<HumidifierDevice>().SingleAsync();
        device.MacAddress.ShouldBe(MacAddress);
        device.LastSeenAt.ShouldBe(_harness.Time.GetUtcNow());
        device.Configuration.Targets.Count.ShouldBe(7);
        ReportedRelayState().ShouldBe(false);
    }

    [Fact]
    public async Task StateAck_UpdatesLastSeenAndReportedStateWithoutAnotherPush()
    {
        await AddDeviceAsync();
        _harness.Time.Advance(TimeSpan.FromMinutes(5));

        await ReceiveAsync(Uplink(HumidifierMessageType.StateAck, relayOn: true));

        using var context = _harness.CreateContext();
        (await context.Devices.SingleAsync()).LastSeenAt.ShouldBe(_harness.Time.GetUtcNow());
        ReportedRelayState().ShouldBe(true);
        A.CallTo(() => _dispatcher.PublishAsync(A<DevicesChangedEvent>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData("01A2000101F5")]
    [InlineData("020001050000000000A2000101F5")]
    [InlineData("010002050000000000A2000101F5")]
    [InlineData("010001020000000000FF00")]
    public async Task InvalidPayload_FailsWithoutRegistering(string hex)
    {
        var result = await ReceiveAsync(Convert.FromHexString(hex));

        result.Error.ShouldBe(ReceiveHumidifierMessageErrors.InvalidPayload);
        using var context = _harness.CreateContext();
        context.Devices.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_WithoutAHumidifier_SendsNothing()
    {
        (await SyncAsync(force: true)).ShouldBeNull();
    }

    [Fact]
    public async Task Sync_WhileItShouldRun_ResendsEveryTime()
    {
        await AddDeviceAsync();
        await ReportRelayAsync(true);

        (await SyncAsync(force: false)).ShouldBe(true);
        (await SyncAsync(force: false)).ShouldBe(true);
    }

    [Fact]
    public async Task Sync_WhenOffAndAcknowledgedOff_OnlySendsWhenForced()
    {
        SetControl(ControlState.Enable, ControlState.Dwell);
        await AddDeviceAsync();
        await ReportRelayAsync(false);

        (await SyncAsync(force: false)).ShouldBeNull();
        (await SyncAsync(force: true)).ShouldBe(false);
    }

    [Fact]
    public async Task Sync_WhenOffButLastAcknowledgedRunning_SendsOffUntilAcknowledged()
    {
        SetControl(ControlState.Enable, ControlState.Dwell);
        await AddDeviceAsync();
        await ReportRelayAsync(true);

        (await SyncAsync(force: false)).ShouldBe(false);
        (await SyncAsync(force: false)).ShouldBe(false);
    }

    [Fact]
    public async Task Sync_UsesTheAcknowledgedRelayStateForHysteresis()
    {
        SetIndoorHumidity(39f);
        await AddDeviceAsync();

        await ReportRelayAsync(false);
        (await SyncAsync(force: true)).ShouldBe(false);

        await ReportRelayAsync(true);
        (await SyncAsync(force: true)).ShouldBe(true);
    }

    [Fact]
    public async Task Sync_WithInvalidStoredConfiguration_SendsOff()
    {
        await AddDeviceAsync();
        using (var context = _harness.CreateContext())
        {
            (await context.Devices.OfType<HumidifierDevice>().SingleAsync()).ConfigurationJson =
                "{not json";
            await context.SaveChangesAsync();
        }
        await ReportRelayAsync(true);

        (await SyncAsync(force: false)).ShouldBe(false);
    }

    private static byte[] Uplink(HumidifierMessageType type, bool relayOn) =>
        DeviceEnvelope
            .Encode(
                (byte)type,
                HmiCbor.Encode(
                    1,
                    writer =>
                    {
                        writer.WriteUInt32(1);
                        writer.WriteBoolean(relayOn);
                    }
                )
            )
            .Single();

    private async Task<Guid> AddDeviceAsync(bool enabled = true)
    {
        using var context = _harness.CreateContext();
        var device = new HumidifierDevice
        {
            Name = "Humidifier",
            MacAddress = MacAddress,
            Enabled = enabled,
            Configuration = new HumidifierConfiguration(),
        };
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device.Id;
    }

    private EvaluateHumidifierHandler CreateEvaluator(Kelvin.Server.Data.KelvinContext context) =>
        new(context, _harness.Time, _controlState, _forecast, _readings);

    private async Task<EvaluateHumidifierResponse> EvaluateAsync(Guid deviceId, bool relayOn)
    {
        using var context = _harness.CreateContext();
        var result = await CreateEvaluator(context)
            .HandleAsync(new EvaluateHumidifierRequest(deviceId, relayOn));
        result.IsSuccess.ShouldBeTrue();
        return result.Value!;
    }

    private async Task<Result> ReceiveAsync(byte[] payload)
    {
        using var context = _harness.CreateContext();
        var handler = new ReceiveHumidifierMessageHandler(
            context,
            _harness.Time,
            _dispatcher,
            _deviceStates
        );
        return await handler.HandleAsync(new ReceiveHumidifierMessageRequest(MacAddress, payload));
    }

    private Task ReportRelayAsync(bool relayOn) =>
        _deviceStates.HandleAsync(
            new UpdateDeviceStateRequest(
                DeviceStateNames.Humidifier,
                true,
                new HumidifierState(relayOn)
            )
        );

    private bool? ReportedRelayState() =>
        _deviceStates
            .HandleAsync(new GetDeviceStatesRequest())
            .Result.Value!.States.TryGetValue(DeviceStateNames.Humidifier, out var state)
            ? ((HumidifierState)state).Enabled
            : null;

    /// <summary>Returns the state sent to the device, or null when nothing was sent.</summary>
    private async Task<bool?> SyncAsync(bool force)
    {
        var outbound = new DeviceOutboundChannel();
        using (var context = _harness.CreateContext())
        {
            var handler = new SyncHumidifierHandler(
                context,
                CreateEvaluator(context),
                _deviceStates,
                outbound,
                NullLogger<SyncHumidifierHandler>.Instance
            );
            (await handler.HandleAsync(new SyncHumidifierRequest(force))).IsSuccess.ShouldBeTrue();
        }

        if (!outbound.TryRead(out var frame))
            return null;

        outbound.TryRead(out _).ShouldBeFalse();
        frame.MacAddress.ShouldBe(MacAddress);
        frame.Payload.Take(FrameTags.Size).ShouldBe(FrameTags.Humidifier);
        DeviceEnvelope
            .TryDecode(
                frame.Payload[FrameTags.Size..],
                out var type,
                out _,
                out _,
                out _,
                out var body
            )
            .ShouldBeTrue();
        type.ShouldBe((byte)HumidifierMessageType.State);
        return HmiCborMap.Parse(body.ToArray()).Boolean(1);
    }

    private void SetControl(ControlState control, ControlState call) =>
        A.CallTo(() =>
                _controlState.HandleAsync(A<GetControlStateRequest>._, A<CancellationToken>._)
            )
            .Returns(
                Result<GetControlStateResponse>.Success(
                    new(control, null, call, null, false, null, null)
                )
            );

    private void SetOutdoorTemperature(float temperatureC) =>
        A.CallTo(() =>
                _forecast.HandleAsync(A<GetWeatherForecastRequest>._, A<CancellationToken>._)
            )
            .Returns(
                Result<GetWeatherForecastResponse>.Success(
                    new(
                        0,
                        0,
                        "UTC",
                        DateTimeOffset.UtcNow,
                        new WeatherCurrent { TemperatureC = temperatureC },
                        []
                    )
                )
            );

    private void SetIndoorHumidity(float average, params float[] sensors) =>
        A.CallTo(() => _readings.HandleAsync(A<GetLatestReadingsRequest>._, A<CancellationToken>._))
            .Returns(
                Result<GetLatestReadingsResponse>.Success(
                    new(
                        new EnvironmentReading
                        {
                            HumidityPercentage = average,
                            Areas = new(
                                sensors.ToDictionary(
                                    _ => Guid.NewGuid(),
                                    humidity => new SensorPacket { HumidityPercentage = humidity }
                                )
                            ),
                        }
                    )
                )
            );
}
