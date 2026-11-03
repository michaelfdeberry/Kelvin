using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Models;
using Kelvin.Server.Services;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Services;

public class FanArbiterTests
{
    private readonly IDispatcher _dispatcher = A.Fake<IDispatcher>();
    private readonly List<UpdateThermostatRequest> _updates = [];
    private readonly Thermostat _thermostat = ThermostatFixtures.CreateThermostat(RunMode.Heating);
    private readonly FanArbiter _arbiter;

    public FanArbiterTests()
    {
        A.CallTo(() =>
                _dispatcher.DispatchAsync(A<UpdateThermostatRequest>._, A<CancellationToken>._)
            )
            .Invokes(
                (UpdateThermostatRequest request, CancellationToken _) =>
                {
                    _updates.Add(request);
                    _thermostat.FanEnabled = request.FanEnabled;
                }
            )
            .Returns(Result.Success());

        _arbiter = new FanArbiter(_dispatcher);
    }

    [Fact]
    public async Task Acquire_FanOff_TurnsItOn_AndReleaseTurnsItOff()
    {
        (
            await _arbiter.AcquireAsync(FanOwner.Recirculation, _thermostat, default)
        ).IsSuccess.ShouldBeTrue();
        (
            await _arbiter.ReleaseAsync(FanOwner.Recirculation, _thermostat, default)
        ).IsSuccess.ShouldBeTrue();

        _updates.Select(u => u.FanEnabled).ShouldBe([true, false]);
    }

    [Fact]
    public async Task Release_WhileAnotherOwnerHoldsTheFan_LeavesItOn()
    {
        await _arbiter.AcquireAsync(FanOwner.Recirculation, _thermostat, default);
        await _arbiter.AcquireAsync(FanOwner.Balancing, _thermostat, default);

        await _arbiter.ReleaseAsync(FanOwner.Recirculation, _thermostat, default);
        _updates.Select(u => u.FanEnabled).ShouldBe([true]);

        await _arbiter.ReleaseAsync(FanOwner.Balancing, _thermostat, default);
        _updates.Select(u => u.FanEnabled).ShouldBe([true, false]);
    }

    [Fact]
    public async Task FanAlreadyOn_IsLeftOnAfterRelease()
    {
        _thermostat.FanEnabled = true;

        await _arbiter.AcquireAsync(FanOwner.Recirculation, _thermostat, default);
        await _arbiter.ReleaseAsync(FanOwner.Recirculation, _thermostat, default);

        _updates.ShouldBeEmpty();
        _thermostat.FanEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Release_WithoutOwnership_DoesNothing()
    {
        _thermostat.FanEnabled = true;

        (
            await _arbiter.ReleaseAsync(FanOwner.Balancing, _thermostat, default)
        ).IsSuccess.ShouldBeTrue();

        _updates.ShouldBeEmpty();
    }

    [Fact]
    public async Task FailedAcquire_DoesNotTakeOwnership()
    {
        A.CallTo(() =>
                _dispatcher.DispatchAsync(A<UpdateThermostatRequest>._, A<CancellationToken>._)
            )
            .Returns(Result.Failure(new Error("Test.Fan", "failed")));

        (
            await _arbiter.AcquireAsync(FanOwner.Recirculation, _thermostat, default)
        ).IsFailure.ShouldBeTrue();
        (
            await _arbiter.ReleaseAsync(FanOwner.Recirculation, _thermostat, default)
        ).IsSuccess.ShouldBeTrue();

        _updates.ShouldBeEmpty();
    }
}
