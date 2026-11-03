using System.Threading.Channels;
using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Notifications;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;
using Kelvin.Server.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kelvin.Server.Tests.TestHelpers;

/// <summary>
/// Drives <see cref="RecirculationService"/> or <see cref="BalancingService"/> through their public
/// <see cref="BackgroundService"/> surface, with the dispatcher and environment channel faked.
/// </summary>
/// <remarks>
/// Synchronization is tied to an observable event rather than assumed scheduling: the service signals each time a
/// loop iteration starts (after its last prelude dispatch), and every helper that moves the service forward waits for
/// the NEXT signal, which can only fire once all work from the previous iteration has finished. Readings are queued
/// in a channel so a push never races with the service's first read.
/// </remarks>
public sealed class FanServiceHarness
{
    private readonly IDispatcher _dispatcher = A.Fake<IDispatcher>();
    private readonly IEnvironmentReadingsChannel _environmentChannel =
        A.Fake<IEnvironmentReadingsChannel>();
    private readonly Channel<EnvironmentReading> _readings =
        Channel.CreateUnbounded<EnvironmentReading>();
    private readonly SemaphoreSlim _iterationStarted = new(0);
    private readonly BackgroundService _service;

    /// <summary>The thermostat the services read. Mutate it in place between pushes.</summary>
    public Thermostat Thermostat { get; } = ThermostatFixtures.CreateThermostat(RunMode.Heating);

    public bool FailThermostatReads { get; set; }

    public bool FailFanUpdates { get; set; }

    public List<UpdateThermostatRequest> FanUpdates { get; } = [];

    public List<SendNotificationRequest> Notifications { get; } = [];

    public List<Guid> SentNotificationIds { get; } = [];

    public List<Guid> DismissedNotificationIds { get; } = [];

    public FakeTimeProvider Time { get; } = new(ThermostatFixtures.Now);

    private FanServiceHarness(
        Func<TimeProvider, IDispatcher, IEnvironmentReadingsChannel, BackgroundService> create
    )
    {
        A.CallTo(() => _environmentChannel.ReadAsync(A<Guid>._, A<CancellationToken>._))
            .ReturnsLazily(() => _readings.Reader.ReadAsync().AsTask());

        A.CallTo(() =>
                _dispatcher.DispatchAsync<GetThermostatRequest, GetThermostatResponse>(
                    A<GetThermostatRequest>._,
                    A<CancellationToken>._
                )
            )
            .ReturnsLazily(() =>
            {
                var result = FailThermostatReads
                    ? Result<GetThermostatResponse>.Failure(new Error("Test.Thermostat", "failed"))
                    : Result<GetThermostatResponse>.Success(new GetThermostatResponse(Thermostat));

                _iterationStarted.Release();

                return result;
            });

        A.CallTo(() =>
                _dispatcher.DispatchAsync(A<UpdateThermostatRequest>._, A<CancellationToken>._)
            )
            .Invokes(
                (UpdateThermostatRequest request, CancellationToken _) => FanUpdates.Add(request)
            )
            .ReturnsLazily(() =>
                FailFanUpdates ? Result.Failure(new Error("Test.Fan", "failed")) : Result.Success()
            );

        A.CallTo(() =>
                _dispatcher.DispatchAsync<SendNotificationRequest, SendNotificationResponse>(
                    A<SendNotificationRequest>._,
                    A<CancellationToken>._
                )
            )
            .ReturnsLazily(
                (SendNotificationRequest request, CancellationToken _) =>
                {
                    var id = Guid.NewGuid();
                    Notifications.Add(request);
                    SentNotificationIds.Add(id);
                    return Result<SendNotificationResponse>.Success(
                        new SendNotificationResponse(id)
                    );
                }
            );

        A.CallTo(() =>
                _dispatcher.DispatchAsync(A<DismissNotificationRequest>._, A<CancellationToken>._)
            )
            .Invokes(
                (DismissNotificationRequest request, CancellationToken _) =>
                    DismissedNotificationIds.Add(request.NotificationId)
            )
            .Returns(Result.Success());

        _service = create(Time, _dispatcher, _environmentChannel);
    }

    public static FanServiceHarness ForRecirculation()
    {
        var harness = new FanServiceHarness(
            (time, dispatcher, channel) =>
                new RecirculationService(
                    NullLogger<RecirculationService>.Instance,
                    time,
                    dispatcher,
                    channel
                )
        );
        harness.Thermostat.CO2RecirculationEnabled = true;
        return harness;
    }

    public static FanServiceHarness ForBalancing()
    {
        var harness = new FanServiceHarness(
            (time, dispatcher, channel) =>
                new BalancingService(
                    NullLogger<BalancingService>.Instance,
                    time,
                    dispatcher,
                    channel
                )
        );
        harness.Thermostat.HeatingAutoBalanceEnabled = true;
        return harness;
    }

    /// <summary>Starts the service and waits until its first iteration has begun.</summary>
    public async Task StartAsync()
    {
        await _service.StartAsync(CancellationToken.None);
        await _iterationStarted.WaitAsync();
    }

    public Task StopAsync() => _service.StopAsync(new CancellationToken(canceled: true));

    /// <summary>Delivers a call transition to the service without waiting for it to react.</summary>
    public void RaiseCallChange(ControlState from, ControlState to) =>
        ((IEventHandler<ControlStateChangedEvent>)_service)
            .HandleAsync(
                new ControlStateChangedEvent(
                    new ControlStateChange
                    {
                        Kind = ControlChangeKind.Call,
                        State = to,
                        PreviousState = from,
                    }
                )
            )
            .GetAwaiter()
            .GetResult();

    /// <summary>Ends a heat call and waits for the service to finish reacting to it.</summary>
    public async Task EndHeatCallAsync()
    {
        RaiseCallChange(ControlState.Heating, ControlState.Dwell);
        await _iterationStarted.WaitAsync();
    }

    /// <summary>Queues a reading without waiting, for iterations that end in a backoff delay.</summary>
    public void Enqueue(EnvironmentReading reading) => _readings.Writer.TryWrite(reading);

    /// <summary>
    /// Queues a reading and waits for the next iteration to begin, so everything the reading caused has happened.
    /// </summary>
    public async Task PushAsync(EnvironmentReading reading)
    {
        Enqueue(reading);
        await _iterationStarted.WaitAsync();
    }

    /// <summary>
    /// Advances the clock and waits for the next iteration to begin. The advance is repeated while no iteration has
    /// started, because a backoff delay may not have been scheduled yet when the first advance happens.
    /// </summary>
    public async Task AdvanceAsync(TimeSpan by)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            Time.Advance(by);
            if (await _iterationStarted.WaitAsync(TimeSpan.FromMilliseconds(50)))
            {
                return;
            }
        }

        throw new TimeoutException("The service did not start another iteration.");
    }
}

public static class FanFixtures
{
    public static SensorPacket Area(float temperatureC, ushort co2Ppm = 0) =>
        new()
        {
            SensorId = Guid.NewGuid(),
            TemperatureC = temperatureC,
            CO2LevelPpm = co2Ppm,
        };

    public static EnvironmentReading Reading(float co2Ppm, params SensorPacket[] areas)
    {
        var reading = new EnvironmentReading
        {
            Timestamp = ThermostatFixtures.Now,
            CO2LevelPpm = co2Ppm,
        };
        foreach (var area in areas)
        {
            reading.Areas[area.SensorId!.Value] = area;
        }

        return reading;
    }

    /// <summary>A reading whose areas span exactly the given temperatures.</summary>
    public static EnvironmentReading Spread(params float[] areaTemperaturesC) =>
        Reading(0, areaTemperaturesC.Select(t => Area(t)).ToArray());
}
