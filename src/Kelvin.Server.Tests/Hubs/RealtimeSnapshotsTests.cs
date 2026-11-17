using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Features.Control;
using Kelvin.Server.Features.Devices;
using Kelvin.Server.Features.Gateways;
using Kelvin.Server.Features.Humidifiers;
using Kelvin.Server.Features.Preferences;
using Kelvin.Server.Features.Sensors;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Features.Weather;
using Kelvin.Server.Hubs;
using Kelvin.Server.Models;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Hubs;

public class RealtimeSnapshotsTests
{
    private static readonly Error Failed = new("Test.Failed", "failed");

    private readonly IHandler<GetControlStateRequest, GetControlStateResponse> _controlState =
        A.Fake<IHandler<GetControlStateRequest, GetControlStateResponse>>();
    private readonly IHandler<GetLatestReadingsRequest, GetLatestReadingsResponse> _readings =
        A.Fake<IHandler<GetLatestReadingsRequest, GetLatestReadingsResponse>>();
    private readonly IHandler<GetThermostatRequest, GetThermostatResponse> _thermostat = A.Fake<
        IHandler<GetThermostatRequest, GetThermostatResponse>
    >();
    private readonly IHandler<GetSetPointsRequest, GetSetPointsResponse> _setPoints = A.Fake<
        IHandler<GetSetPointsRequest, GetSetPointsResponse>
    >();
    private readonly IHandler<GetSchedulesRequest, GetSchedulesResponse> _schedules = A.Fake<
        IHandler<GetSchedulesRequest, GetSchedulesResponse>
    >();
    private readonly IHandler<GetSensorsRequest, GetSensorsResponse> _sensors = A.Fake<
        IHandler<GetSensorsRequest, GetSensorsResponse>
    >();
    private readonly IHandler<GetDevicesRequest, GetDevicesResponse> _devices = A.Fake<
        IHandler<GetDevicesRequest, GetDevicesResponse>
    >();
    private readonly IHandler<GetHumidifiersRequest, GetHumidifiersResponse> _humidifiers = A.Fake<
        IHandler<GetHumidifiersRequest, GetHumidifiersResponse>
    >();
    private readonly IHandler<GetRelayStatesRequest, GetRelayStatesResponse> _relayStates = A.Fake<
        IHandler<GetRelayStatesRequest, GetRelayStatesResponse>
    >();
    private readonly IHandler<GetWeatherForecastRequest, GetWeatherForecastResponse> _weather =
        A.Fake<IHandler<GetWeatherForecastRequest, GetWeatherForecastResponse>>();
    private readonly IHandler<GetDeviceStatesRequest, GetDeviceStatesResponse> _deviceStates =
        A.Fake<IHandler<GetDeviceStatesRequest, GetDeviceStatesResponse>>();
    private readonly IHandler<GetPreferencesRequest, GetPreferencesResponse> _preferences = A.Fake<
        IHandler<GetPreferencesRequest, GetPreferencesResponse>
    >();

    private RealtimeSnapshots CreateSnapshots(ControlStateChangeDto? lastChange = null)
    {
        A.CallTo(() =>
                _controlState.HandleAsync(A<GetControlStateRequest>._, A<CancellationToken>._)
            )
            .Returns(
                Result<GetControlStateResponse>.Success(
                    new GetControlStateResponse(
                        ControlState.Disable,
                        null,
                        ControlState.Dwell,
                        null,
                        false,
                        null,
                        lastChange
                    )
                )
            );
        A.CallTo(() => _readings.HandleAsync(A<GetLatestReadingsRequest>._, A<CancellationToken>._))
            .Returns(
                Result<GetLatestReadingsResponse>.Success(
                    new GetLatestReadingsResponse(new EnvironmentReading())
                )
            );
        A.CallTo(() => _thermostat.HandleAsync(A<GetThermostatRequest>._, A<CancellationToken>._))
            .Returns(
                Result<GetThermostatResponse>.Success(
                    new GetThermostatResponse(new Models.Thermostat { Mode = RunMode.Heating })
                )
            );
        A.CallTo(() => _setPoints.HandleAsync(A<GetSetPointsRequest>._, A<CancellationToken>._))
            .Returns(Result<GetSetPointsResponse>.Success(new GetSetPointsResponse([])));
        A.CallTo(() => _schedules.HandleAsync(A<GetSchedulesRequest>._, A<CancellationToken>._))
            .Returns(Result<GetSchedulesResponse>.Success(new GetSchedulesResponse([])));
        A.CallTo(() => _sensors.HandleAsync(A<GetSensorsRequest>._, A<CancellationToken>._))
            .Returns(Result<GetSensorsResponse>.Success(new GetSensorsResponse([])));
        A.CallTo(() => _devices.HandleAsync(A<GetDevicesRequest>._, A<CancellationToken>._))
            .Returns(Result<GetDevicesResponse>.Success(new GetDevicesResponse([])));
        A.CallTo(() => _humidifiers.HandleAsync(A<GetHumidifiersRequest>._, A<CancellationToken>._))
            .Returns(Result<GetHumidifiersResponse>.Success(new GetHumidifiersResponse([])));
        A.CallTo(() => _relayStates.HandleAsync(A<GetRelayStatesRequest>._, A<CancellationToken>._))
            .Returns(
                Result<GetRelayStatesResponse>.Success(
                    new GetRelayStatesResponse(null, null, null, null)
                )
            );
        A.CallTo(() =>
                _weather.HandleAsync(
                    A<GetWeatherForecastRequest>.That.Matches(r => r.CachedOnly),
                    A<CancellationToken>._
                )
            )
            .Returns(
                Result<GetWeatherForecastResponse>.Success(
                    new GetWeatherForecastResponse(0, 0, "UTC", DateTimeOffset.UtcNow, null, [])
                )
            );
        A.CallTo(() =>
                _deviceStates.HandleAsync(A<GetDeviceStatesRequest>._, A<CancellationToken>._)
            )
            .Returns(Result<GetDeviceStatesResponse>.Success(new GetDeviceStatesResponse([])));
        A.CallTo(() => _preferences.HandleAsync(A<GetPreferencesRequest>._, A<CancellationToken>._))
            .Returns(
                Result<GetPreferencesResponse>.Success(
                    new GetPreferencesResponse(
                        TemperatureUnit.Celsius,
                        TimeFormat.Hour24,
                        null,
                        null
                    )
                )
            );

        return new RealtimeSnapshots(
            _controlState,
            _readings,
            _thermostat,
            _setPoints,
            _schedules,
            _sensors,
            _devices,
            _humidifiers,
            _deviceStates,
            _relayStates,
            _weather,
            _preferences
        );
    }

    [Fact]
    public async Task All_ContainsEveryKindOfDataOnceControlHasBeenActuated()
    {
        var change = ControlStateChangeDto.FromEntity(
            new ControlStateChange { Kind = ControlChangeKind.Call, State = ControlState.Heating }
        );

        var messages = await CreateSnapshots(change).AllAsync();

        messages
            .Select(m => m.Type)
            .ShouldBe([
                RealtimeMessageTypes.ControlStateChanged,
                RealtimeMessageTypes.EnvironmentReadingChanged,
                RealtimeMessageTypes.ThermostatSettingsChanged,
                RealtimeMessageTypes.SensorsSettingsChanged,
                RealtimeMessageTypes.DevicesSettingsChanged,
                RealtimeMessageTypes.DeviceStateChanged,
                RealtimeMessageTypes.RelayStatesChanged,
                RealtimeMessageTypes.WeatherForecastChanged,
                RealtimeMessageTypes.PreferencesChanged,
            ]);
        messages[0].Payload.ShouldBe(change);
    }

    [Fact]
    public async Task All_SkipsControlStateWhenNothingHasBeenActuated()
    {
        var messages = await CreateSnapshots().AllAsync();

        messages.ShouldNotContain(m => m.Type == RealtimeMessageTypes.ControlStateChanged);
        messages.Count.ShouldBe(8);
    }

    [Fact]
    public async Task Thermostat_CombinesThermostatSetPointsAndSchedules()
    {
        var message = (await CreateSnapshots().AllAsync()).Single(m =>
            m.Type == RealtimeMessageTypes.ThermostatSettingsChanged
        );

        var payload = message.Payload.ShouldBeOfType<ThermostatSettingsPayload>();
        payload.Thermostat.Mode.ShouldBe(RunMode.Heating);
    }

    [Fact]
    public async Task Devices_IsSkippedWhenAnyPartCannotBeRead()
    {
        var snapshots = CreateSnapshots();
        A.CallTo(() => _humidifiers.HandleAsync(A<GetHumidifiersRequest>._, A<CancellationToken>._))
            .Returns(Result<GetHumidifiersResponse>.Failure(Failed));

        (await snapshots.AllAsync()).ShouldNotContain(m =>
            m.Type == RealtimeMessageTypes.DevicesSettingsChanged
        );
    }
}
