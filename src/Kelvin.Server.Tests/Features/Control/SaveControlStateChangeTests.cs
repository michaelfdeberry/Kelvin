using FakeItEasy;
using Kelvin.Server.Features.Control;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
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
    [Fact]
    public async Task PersistsChange_AndBroadcastsToClients()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();

        var logger = A.Fake<ILogger<SaveControlStateChangeHandler>>();
        var handler = new SaveControlStateChangeHandler(context, logger);

        var change = ControlStateChangeFixtures.CreateCall(ControlState.Heating);
        var result = await handler.HandleAsync(new SaveControlStateChangeRequest(change));

        result.IsSuccess.ShouldBeTrue();
        context.ControlStateChanges.Single().State.ShouldBe(ControlState.Heating);
    }

    [Fact]
    public async Task WhenBroadcastThrows_StillReturnsSuccess_ChangeIsPersisted()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();

        var logger = A.Fake<ILogger<SaveControlStateChangeHandler>>();
        var handler = new SaveControlStateChangeHandler(context, logger);

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

        var logger = A.Fake<ILogger<SaveControlStateChangeHandler>>();
        var handler = new SaveControlStateChangeHandler(context, logger);

        var change = ControlStateChangeFixtures.CreateCall(ControlState.Heating);
        var result = await handler.HandleAsync(new SaveControlStateChangeRequest(change));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SaveControlStateChangeErrors.DefaultError);
    }
}
