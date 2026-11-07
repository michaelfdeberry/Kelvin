using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Features.Devices;
using Kelvin.Server.Messaging;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Devices;

public class DeviceStateTests
{
    private readonly IDispatcher _dispatcher = A.Fake<IDispatcher>();
    private readonly DeviceStateHandler _handler;

    public DeviceStateTests() => _handler = new DeviceStateHandler(_dispatcher);

    private Task<Result> UpdateAsync(bool deviceEnabled, bool running) =>
        _handler.HandleAsync(
            new UpdateDeviceStateRequest(
                DeviceStateNames.Humidifier,
                deviceEnabled,
                new HumidifierState(running)
            )
        );

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
    public async Task Update_DisabledDevice_IsNotBroadcastAndDropsCachedState()
    {
        await UpdateAsync(true, true);

        await UpdateAsync(false, true);

        MustHaveBroadcast(1);
        (await _handler.HandleAsync(new GetDeviceStatesRequest())).Value!.States.ShouldBeEmpty();
    }
}
