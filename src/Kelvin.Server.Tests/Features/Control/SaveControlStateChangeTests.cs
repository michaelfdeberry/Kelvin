using FakeItEasy;
using Kelvin.Server.Features.Control;
using Kelvin.Server.Hubs;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Control;

/// <summary>
/// Tests for <see cref="SaveControlStateChangeHandler"/>, which persists a change and then broadcasts it over
/// SignalR - the DB write is committed first so a lost broadcast never loses data.
/// </summary>
public class SaveControlStateChangeTests
{
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

    [Fact]
    public async Task PersistsChange_AndBroadcastsToClients()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();

        var (hub, clientProxy) = CreateFakeHub();
        var logger = A.Fake<ILogger<SaveControlStateChangeHandler>>();
        var handler = new SaveControlStateChangeHandler(context, hub, logger);

        var change = ControlStateChangeFixtures.CreateCall(ControlState.Heating);
        var result = await handler.HandleAsync(new SaveControlStateChangeRequest(change));

        result.IsSuccess.ShouldBeTrue();
        context.ControlStateChanges.Single().State.ShouldBe(ControlState.Heating);
        A.CallTo(() => clientProxy.ControlStateChanged(A<ControlStateChangeDto>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task WhenBroadcastThrows_StillReturnsSuccess_ChangeIsPersisted()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();

        var (hub, clientProxy) = CreateFakeHub();
        A.CallTo(() => clientProxy.ControlStateChanged(A<ControlStateChangeDto>._))
            .Throws(new InvalidOperationException("boom"));

        var logger = A.Fake<ILogger<SaveControlStateChangeHandler>>();
        var handler = new SaveControlStateChangeHandler(context, hub, logger);

        var change = ControlStateChangeFixtures.CreateCall(ControlState.Heating);
        var result = await handler.HandleAsync(new SaveControlStateChangeRequest(change));

        result.IsSuccess.ShouldBeTrue();
        context.ControlStateChanges.Single().State.ShouldBe(ControlState.Heating);
    }

    [Fact]
    public async Task WhenSaveFails_ReturnsFailure_AndDoesNotBroadcast()
    {
        using var harness = new KelvinContextHarness();
        var context = harness.CreateContext();
        // Disposing the context makes any further operation on it throw ObjectDisposedException - a reliable way
        // to force SaveChangesAsync to fail without depending on SQLite connection-lifetime specifics.
        await context.DisposeAsync();

        var (hub, clientProxy) = CreateFakeHub();
        var logger = A.Fake<ILogger<SaveControlStateChangeHandler>>();
        var handler = new SaveControlStateChangeHandler(context, hub, logger);

        var change = ControlStateChangeFixtures.CreateCall(ControlState.Heating);
        var result = await handler.HandleAsync(new SaveControlStateChangeRequest(change));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SaveControlStateChangeErrors.DefaultError);
        A.CallTo(() => clientProxy.ControlStateChanged(A<ControlStateChangeDto>._))
            .MustNotHaveHappened();
    }
}
