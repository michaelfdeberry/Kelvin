using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Channels;
using Kelvin.Server.Features.Control;
using Kelvin.Server.Features.Gateways;
using Kelvin.Server.Hubs;
using Kelvin.Server.Models;
using Kelvin.Server.Services;
using Microsoft.AspNetCore.SignalR;
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
    private GetLatestControlStateChangeResponse? _latestLifecycle;

    public IRelayController Relays { get; } = A.Fake<IRelayController>();

    public FakeTimeProvider Time { get; } = new();

    /// <summary>
    /// The state changes the service asked to have recorded, in the order it queued them. Only safe to assert on
    /// once <see cref="PushAsync"/> or <see cref="AdvanceAsync"/> has returned, for the same reason the relay
    /// calls are.
    /// </summary>
    public List<ControlStateChange> RecordedChanges { get; } = [];

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

        SetRecordingResult(Result.Success());
        SetLatestLifecycleState(null);

        SetGateway(ControlFixtures.CreateGateway());
        var (hub, clientProxy) = CreateFakeHub();
        _service = new ControlService(
            NullLogger<ControlService>.Instance,
            _controlChannel,
            _dispatcher,
            Relays,
            Time,
            _lifetime,
            hub
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

    public void SetLatestLifecycleState(GetLatestControlStateChangeResponse? latestLifecycle)
    {
        _latestLifecycle = latestLifecycle;

        A.CallTo(() =>
                _dispatcher.DispatchAsync<
                    GetLatestControlStateChangeRequest,
                    GetLatestControlStateChangeResponse?
                >(A<GetLatestControlStateChangeRequest>._, A<CancellationToken>._)
            )
            .ReturnsLazily(() =>
                Result<GetLatestControlStateChangeResponse?>.Success(_latestLifecycle)
            );
    }

    /// <summary>
    /// Captures every state change the service dispatches for recording, and controls what the save reports back.
    /// </summary>
    /// <remarks>
    /// This fake deliberately does NOT release <c>_iterationStarted</c>. That signal has to stay tied to the
    /// gateway dispatch, which happens exactly once per loop iteration; recording happens a variable number of
    /// times per message, so keying the harness off it would let assertions run mid-iteration.
    /// </remarks>
    public void SetRecordingResult(Result result) =>
        A.CallTo(() =>
                _dispatcher.DispatchAsync(
                    A<SaveControlStateChangeRequest>._,
                    A<CancellationToken>._
                )
            )
            .ReturnsLazily(
                (SaveControlStateChangeRequest request, CancellationToken _) =>
                {
                    RecordedChanges.Add(request.Change);
                    return result;
                }
            );

    /// <summary>Starts the service and waits until it is blocked on its first control channel read.</summary>
    public async Task StartAsync(bool clearRecordedChanges = true)
    {
        await _service.StartAsync(CancellationToken.None);
        await _iterationStarted.WaitAsync();

        if (clearRecordedChanges)
            RecordedChanges.Clear();
    }

    public Task StopAsync() => _service.StopAsync(new CancellationToken(canceled: true));

    /// <summary>
    /// Delivers a control message carrying producer context, then waits for the next iteration.
    /// </summary>
    public async Task PushAsync(ControlContext context)
    {
        Deliver(context);
        await _iterationStarted.WaitAsync();
    }

    /// <summary>
    /// Delivers a control message without waiting for the next iteration, for cases where the service is not
    /// expected to loop again (an unusable GPIO stops the service).
    /// </summary>
    public void Deliver(ControlContext context)
    {
        var pending =
            _pendingRead
            ?? throw new InvalidOperationException(
                "The service is not currently awaiting a control message."
            );
        _pendingRead = null;
        pending.SetResult(new ControlMessage(context));
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

    private static (
        IHubContext<ControlHub, IControlClient> Hub,
        IControlClient ClientProxy
    ) CreateFakeHub()
    {
        var clientProxy = A.Fake<IControlClient>();
        var clients = A.Fake<IHubClients<IControlClient>>();
        A.CallTo(() => clients.All).Returns(clientProxy);

        var hub = A.Fake<IHubContext<ControlHub, IControlClient>>();
        A.CallTo(() => hub.Clients).Returns(clients);

        return (hub, clientProxy);
    }
}
