using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.GeoCoding;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Features.Weather;
using Kelvin.Server.Models;
using Kelvin.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kelvin.Server.Tests.TestHelpers;

/// <summary>
/// Drives a <see cref="ThermostatService"/> instance through its public <see cref="BackgroundService"/> surface
/// (StartAsync/StopAsync), with all constructor dependencies faked via FakeItEasy.
/// </summary>
/// <remarks>
/// The environment channel read is deliberately backed by a fresh, not-yet-completed
/// <see cref="TaskCompletionSource{TResult}"/> per call, paired with a <see cref="SemaphoreSlim"/> that is
/// released every time the service asks for a new reading. Completing a pending read via
/// <c>TaskCompletionSource.SetResult</c> does not guarantee that the rest of that loop iteration (dispatcher
/// calls, control channel writes) has finished running by the time <c>SetResult</c> returns - whether it does is
/// an internal implementation detail of the TPL/BackgroundService that must not be relied upon. Instead,
/// <see cref="PushEnvironmentAsync"/> completes the current pending read and then waits for the *next* read
/// request signal, which can only be raised once the service has looped all the way back around - i.e. once all
/// of that iteration's writes are guaranteed to have already happened. This makes the synchronization correct
/// regardless of the actual threading/continuation behavior involved, with no timeouts or wall-clock waiting
/// required.
/// </remarks>
public sealed class ThermostatServiceHarness
{
    private readonly IControlChannel _controlChannel = A.Fake<IControlChannel>();
    private readonly IEnvironmentChannel _environmentChannel = A.Fake<IEnvironmentChannel>();
    private readonly IDispatcher _dispatcher = A.Fake<IDispatcher>();
    private readonly ThermostatService _service;
    private readonly SemaphoreSlim _environmentReadRequested = new(0);
    private TaskCompletionSource<Kelvin.Server.Models.Environment>? _pendingEnvironmentRead;

    public List<ControlMessage> WrittenMessages { get; } = [];

    public ThermostatServiceHarness()
    {
        A.CallTo(() => _environmentChannel.ReadAsync(A<Guid>._, A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                var tcs = new TaskCompletionSource<Kelvin.Server.Models.Environment>();
                _pendingEnvironmentRead = tcs;
                _environmentReadRequested.Release();
                return tcs.Task;
            });

        A.CallTo(() => _controlChannel.WriteAsync(A<ControlMessage>._, A<CancellationToken>._))
            .Invokes((ControlMessage message, CancellationToken _) => WrittenMessages.Add(message))
            .Returns(Task.CompletedTask);

        // Sensible default so tests that don't care about weather forecasting don't need to configure it explicitly.
        SetWeatherFailure(GetCurrentLocationErrors.LocationNotConfigured);

        _service = new ThermostatService(
            _controlChannel,
            _environmentChannel,
            _dispatcher,
            NullLogger<ThermostatService>.Instance
        );
    }

    public void SetThermostat(Thermostat thermostat) =>
        A.CallTo(() =>
                _dispatcher.DispatchAsync<GetThermostatRequest, GetThermostatResponse>(
                    A<GetThermostatRequest>._,
                    A<CancellationToken>._
                )
            )
            .Returns(Result<GetThermostatResponse>.Success(new GetThermostatResponse(thermostat)));

    public void SetThermostatFailure(Error error) =>
        A.CallTo(() =>
                _dispatcher.DispatchAsync<GetThermostatRequest, GetThermostatResponse>(
                    A<GetThermostatRequest>._,
                    A<CancellationToken>._
                )
            )
            .Returns(Result<GetThermostatResponse>.Failure(error));

    /// <summary>
    /// Configures a successful weather forecast response with the given current temperature. Pass null to indicate
    /// there is no forecast temperature available (matches production behavior when Current is null).
    /// </summary>
    public void SetWeatherForecast(double? temperatureC)
    {
        var current = temperatureC is null
            ? null
            : new WeatherCurrent { TemperatureC = temperatureC.Value };
        var response = new GetWeatherForecastResponse(
            0,
            0,
            "UTC",
            DateTimeOffset.UtcNow,
            current,
            []
        );
        A.CallTo(() =>
                _dispatcher.DispatchAsync<GetWeatherForecastRequest, GetWeatherForecastResponse>(
                    A<GetWeatherForecastRequest>._,
                    A<CancellationToken>._
                )
            )
            .Returns(Result<GetWeatherForecastResponse>.Success(response));
    }

    public void SetWeatherFailure(Error error) =>
        A.CallTo(() =>
                _dispatcher.DispatchAsync<GetWeatherForecastRequest, GetWeatherForecastResponse>(
                    A<GetWeatherForecastRequest>._,
                    A<CancellationToken>._
                )
            )
            .Returns(Result<GetWeatherForecastResponse>.Failure(error));

    /// <summary>
    /// Starts the service and waits until it is blocked on its first environment read.
    /// </summary>
    public async Task StartAsync()
    {
        await _service.StartAsync(CancellationToken.None);
        await _environmentReadRequested.WaitAsync();
    }

    public Task StopAsync() => _service.StopAsync(new CancellationToken(canceled: true));

    /// <summary>
    /// Completes the pending environment read, then waits until the service has looped all the way back around to
    /// request another reading - guaranteeing that every dispatcher call and control channel write belonging to
    /// that loop iteration has already happened by the time this method returns.
    /// </summary>
    public async Task PushEnvironmentAsync(Kelvin.Server.Models.Environment environment)
    {
        var pending =
            _pendingEnvironmentRead
            ?? throw new InvalidOperationException(
                "The service is not currently awaiting an environment reading."
            );
        _pendingEnvironmentRead = null;
        pending.SetResult(environment);
        await _environmentReadRequested.WaitAsync();
    }
}
