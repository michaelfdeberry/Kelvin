using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Features.Devices;
using Kelvin.Server.Messaging;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Devices;

public class DeviceStateTests
{
    private readonly IDispatcher _dispatcher = A.Fake<IDispatcher>();
    private readonly FakeTimeProvider _time = new();
    private readonly DeviceStateHandler _handler;

    public DeviceStateTests() => _handler = new DeviceStateHandler(_dispatcher, _time);

    private Task<Result> UpdateAsync(bool deviceEnabled, bool running, bool commanded = false) =>
        _handler.HandleAsync(
            new UpdateDeviceStateRequest(
                DeviceStateNames.Humidifier,
                deviceEnabled,
                new HumidifierState(running),
                commanded
            )
        );

    private async Task<bool?> CurrentAsync() =>
        (await _handler.HandleAsync(new GetDeviceStatesRequest())).Value!.States.TryGetValue(
            DeviceStateNames.Humidifier,
            out var state
        )
            ? ((HumidifierState)state).Enabled
            : null;

    private static readonly TimeSpan PastTimeout = HumidifierState.ReportTimeout + TimeSpan.FromSeconds(1);

    private void MustHaveBroadcast(int times) =>
        A.CallTo(() =>
                _dispatcher.PublishAsync(A<DeviceStateChangedEvent>._, A<CancellationToken>._)
            )
            .MustHaveHappened(times, Times.Exactly);

    [Fact]
    public async Task Update_CachesAndBroadcastsTheFullStateSet()
    {
        await UpdateAsync(true, true);

        A.CallTo(() =>
                _dispatcher.PublishAsync(
                    A<DeviceStateChangedEvent>.That.Matches(e =>
                        e.States.Count == 1
                        && (HumidifierState)e.States[DeviceStateNames.Humidifier]
                            == new HumidifierState(true)
                    ),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        var cached = await _handler.HandleAsync(new GetDeviceStatesRequest());
        cached.Value!.States[DeviceStateNames.Humidifier].ShouldBe(new HumidifierState(true));
    }

    [Fact]
    public async Task Update_UnchangedState_IsNotBroadcastAgain()
    {
        await UpdateAsync(true, true);
        await UpdateAsync(true, true);
        MustHaveBroadcast(1);

        await UpdateAsync(true, false);
        MustHaveBroadcast(2);
    }

    [Fact]
    public async Task Update_DisabledDevice_DropsCachedStateAndBroadcastsTheRemoval()
    {
        await UpdateAsync(true, true);

        await UpdateAsync(false, true);

        MustHaveBroadcast(2);
        (await _handler.HandleAsync(new GetDeviceStatesRequest())).Value!.States.ShouldBeEmpty();
    }

    [Fact]
    public async Task Update_DisabledDeviceWithNoCachedState_IsNotBroadcast()
    {
        await UpdateAsync(false, true);

        MustHaveBroadcast(0);
    }

    [Fact]
    public async Task Expire_StateNotReportedWithinTheTimeout_RevertsToDefaultAndBroadcasts()
    {
        await UpdateAsync(true, true);
        _time.Advance(PastTimeout);

        await _handler.HandleAsync(new ExpireDeviceStatesRequest());

        (await CurrentAsync()).ShouldBe(false);
        MustHaveBroadcast(2);

        await _handler.HandleAsync(new ExpireDeviceStatesRequest());
        MustHaveBroadcast(2);
    }

    [Fact]
    public async Task Expire_ReportedStateIsKeptAlive()
    {
        await UpdateAsync(true, true);
        _time.Advance(PastTimeout - TimeSpan.FromSeconds(30));
        await UpdateAsync(true, true);
        _time.Advance(PastTimeout - TimeSpan.FromSeconds(30));

        await _handler.HandleAsync(new ExpireDeviceStatesRequest());

        (await CurrentAsync()).ShouldBe(true);
        MustHaveBroadcast(1);
    }

    [Fact]
    public async Task Get_StateThatTimedOutBeforeAnExpiryPass_ReadsAsDefault()
    {
        await UpdateAsync(true, true);
        _time.Advance(PastTimeout);

        (await CurrentAsync()).ShouldBe(false);
    }

    [Fact]
    public async Task Update_CommandedState_DoesNotKeepItselfAlive()
    {
        await UpdateAsync(true, false);
        await UpdateAsync(true, true, commanded: true);
        (await CurrentAsync()).ShouldBe(true);

        _time.Advance(PastTimeout - TimeSpan.FromSeconds(30));
        await UpdateAsync(true, true, commanded: true);
        _time.Advance(TimeSpan.FromSeconds(31));
        await _handler.HandleAsync(new ExpireDeviceStatesRequest());

        (await CurrentAsync()).ShouldBe(false);
    }

    [Fact]
    public async Task Update_CommandedStateAfterATimeout_IsIgnoredUntilTheDeviceReports()
    {
        await UpdateAsync(true, true);
        _time.Advance(PastTimeout);
        await _handler.HandleAsync(new ExpireDeviceStatesRequest());

        await UpdateAsync(true, true, commanded: true);
        (await CurrentAsync()).ShouldBe(false);

        await UpdateAsync(true, true);
        (await CurrentAsync()).ShouldBe(true);
        await UpdateAsync(true, true, commanded: true);
        (await CurrentAsync()).ShouldBe(true);
    }

    [Fact]
    public void BroadcastState_DoesNotExposeTheTimingProperties()
    {
        System.Text.Json.JsonSerializer.Serialize<object>(new HumidifierState(true)).ShouldBe("{\"Enabled\":true}");
    }
}
