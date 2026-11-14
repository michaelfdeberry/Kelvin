using System.Threading.Channels;
using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Features.Humidifiers;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;
using Kelvin.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Services;

public class DeviceIntegrationServiceTests
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private readonly FakeTimeProvider _time = new();
    private readonly IDispatcher _dispatcher = A.Fake<IDispatcher>();
    private readonly Channel<bool> _syncs = Channel.CreateUnbounded<bool>();
    private readonly DeviceIntegrationService _service;

    public DeviceIntegrationServiceTests()
    {
        A.CallTo(() =>
                _dispatcher.DispatchAsync(A<SyncHumidifierRequest>._, A<CancellationToken>._)
            )
            .ReturnsLazily(
                (SyncHumidifierRequest request, CancellationToken _) =>
                {
                    _syncs.Writer.TryWrite(request.Force);
                    return Task.FromResult(Result.Success());
                }
            );
        _service = new DeviceIntegrationService(
            NullLogger<DeviceIntegrationService>.Instance,
            _time,
            _dispatcher
        );
    }

    [Fact]
    public async Task Startup_ForcesASync()
    {
        await _service.StartAsync(CancellationToken.None);

        (await NextSyncAsync()).ShouldBeTrue();

        await _service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CallChangesAndDeviceChanges_ForceASync_ButOtherChangesDoNot()
    {
        await _service.StartAsync(CancellationToken.None);
        await NextSyncAsync();

        await _service.HandleAsync(Change(ControlChangeKind.Fan, ControlState.FanOn));
        await _service.HandleAsync(Change(ControlChangeKind.Call, ControlState.Heating));
        (await NextSyncAsync()).ShouldBeTrue();

        await _service.HandleAsync(new DevicesChangedEvent());
        (await NextSyncAsync()).ShouldBeTrue();

        _syncs.Reader.TryRead(out _).ShouldBeFalse();
        await _service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RefreshInterval_SyncsWithoutForcing()
    {
        await _service.StartAsync(CancellationToken.None);
        await NextSyncAsync();

        // The loop may not have started waiting yet, so keep advancing until the timer is observed.
        bool? force = null;
        for (var attempt = 0; attempt < 50 && force is null; attempt++)
        {
            _time.Advance(DeviceIntegrationService.RefreshInterval);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            try
            {
                force = await _syncs.Reader.ReadAsync(timeout.Token);
            }
            catch (OperationCanceledException) { }
        }

        force.ShouldBe(false);
        await _service.StopAsync(CancellationToken.None);
    }

    private async Task<bool> NextSyncAsync()
    {
        using var timeout = new CancellationTokenSource(WaitTimeout);
        return await _syncs.Reader.ReadAsync(timeout.Token);
    }

    private static ControlStateChangedEvent Change(ControlChangeKind kind, ControlState state) =>
        new(new ControlStateChange { Kind = kind, State = state });
}
