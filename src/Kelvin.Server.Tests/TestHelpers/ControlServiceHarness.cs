using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Gateways;
using Kelvin.Server.Models;
using Kelvin.Server.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kelvin.Server.Tests.TestHelpers;

/// <summary>
/// Drives a <see cref="ControlService"/> instance through its public <see cref="BackgroundService"/> surface
/// (StartAsync/StopAsync). The channel, dispatcher, relay controller and application lifetime are FakeItEasy
/// fakes, and the clock is a <see cref="FakeTimeProvider"/> so the minimum on/off duration guards can be driven
/// without wall-clock waiting.
/// </summary>
/// <remarks>
/// The control channel read is deliberately backed by a fresh, not-yet-completed
/// <see cref="TaskCompletionSource{TResult}"/> per call, paired with a <see cref="SemaphoreSlim"/> that is
/// released every time the service starts another loop iteration (observed through the gateway dispatch, which
/// happens once per iteration - the read itself is kept outstanding across iterations). Completing a pending read
/// via <c>TaskCompletionSource.SetResult</c> does not guarantee that the rest of that loop iteration has finished
/// running by the time <c>SetResult</c> returns - that is an internal implementation detail of the
/// TPL/BackgroundService that must not be relied upon. Instead <see cref="PushAsync"/> completes the current
/// pending read and then waits for the *next* iteration to start, which can only happen once every relay call
/// belonging to the previous one has already happened.
/// </remarks>
public sealed class ControlServiceHarness
{
    private readonly IControlChannel _controlChannel = A.Fake<IControlChannel>();
    private readonly IDispatcher _dispatcher = A.Fake<IDispatcher>();
    private readonly IHostApplicationLifetime _lifetime = A.Fake<IHostApplicationLifetime>();
    private readonly ControlService _service;
    private readonly SemaphoreSlim _iterationStarted = new(0);
    private readonly TaskCompletionSource _stopApplicationRequested = new();
    private TaskCompletionSource<ControlMessage>? _pendingRead;

    public IRelayController Relays { get; } = A.Fake<IRelayController>();

    public FakeTimeProvider Time { get; } = new();

    /// <summary>How many reads the service has started. A reused outstanding read does not increment this.</summary>
    public int ReadCount { get; private set; }

    /// <summary>Completes when the service asks the host to shut down after an unusable GPIO.</summary>
    public Task StopApplicationRequested => _stopApplicationRequested.Task;

    public ControlServiceHarness()
    {
        A.CallTo(() => _controlChannel.ReadAsync(A<Guid>._, A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                var tcs = new TaskCompletionSource<ControlMessage>();
                _pendingRead = tcs;
                ReadCount++;
                return tcs.Task;
            });

        A.CallTo(() => _lifetime.StopApplication())
            .Invokes(() => _stopApplicationRequested.TrySetResult());

        SetGateway(ControlFixtures.CreateGateway());

        _service = new ControlService(
            NullLogger<ControlService>.Instance,
            _controlChannel,
            _dispatcher,
            Relays,
            Time,
            _lifetime
        );
    }

    public void SetGateway(GetGatewayResponse gateway) =>
        A.CallTo(() =>
                _dispatcher.DispatchAsync<GetGatewayRequest, GetGatewayResponse>(
                    A<GetGatewayRequest>._,
                    A<CancellationToken>._
                )
            )
            .ReturnsLazily(() =>
            {
                _iterationStarted.Release();
                return Result<GetGatewayResponse>.Success(gateway);
            });

    /// <summary>Starts the service and waits until it is blocked on its first control channel read.</summary>
    public async Task StartAsync()
    {
        await _service.StartAsync(CancellationToken.None);
        await _iterationStarted.WaitAsync();
    }

    public Task StopAsync() => _service.StopAsync(new CancellationToken(canceled: true));

    /// <summary>
    /// Delivers a control message, then waits until the service has looped all the way back around into its next
    /// iteration - guaranteeing every relay call belonging to that message has already happened.
    /// </summary>
    public async Task PushAsync(ControlState state)
    {
        Deliver(state);
        await _iterationStarted.WaitAsync();
    }

    /// <summary>
    /// Delivers a control message without waiting for the next iteration, for cases where the service is not
    /// expected to loop again (an unusable GPIO stops the service).
    /// </summary>
    public void Deliver(ControlState state)
    {
        var pending =
            _pendingRead
            ?? throw new InvalidOperationException(
                "The service is not currently awaiting a control message."
            );
        _pendingRead = null;
        pending.SetResult(new ControlMessage(state));
    }

    /// <summary>
    /// Advances the clock, then waits until the service has looped back around into its next iteration - which
    /// only happens once a scheduled dwell transition that came due has been applied.
    /// </summary>
    public async Task AdvanceAsync(TimeSpan delay)
    {
        Time.Advance(delay);
        await _iterationStarted.WaitAsync();
    }
}
