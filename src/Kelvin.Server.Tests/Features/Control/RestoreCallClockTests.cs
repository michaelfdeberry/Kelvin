using Kelvin.Server.Application;
using Kelvin.Server.Features.Control;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Control;

public class RestoreCallClockTests
{
    [Fact]
    public async Task RegisteredHandler_RestoresClocksThroughDispatcherAndHistoryHandlers()
    {
        using var harness = new KelvinContextHarness();
        var endedAt = harness.Time.GetUtcNow();
        await using (var context = harness.CreateContext())
        {
            context.ControlStateChanges.Add(
                ControlStateChangeFixtures.CreateCall(ControlState.Dwell, ControlState.Heating)
            );
            await context.SaveChangesAsync();
        }
        harness.Time.Advance(TimeSpan.FromMinutes(7));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(harness.Time);
        services.AddScoped(_ => harness.CreateContext());
        services.AddSingleton<IDispatcher, Dispatcher>();
        services.RegisterDependencies();
        await using var provider = services.BuildServiceProvider();

        var dispatcher = provider.GetRequiredService<IDispatcher>();
        var result = await dispatcher.DispatchAsync<RestoreCallClockRequest, RestoreCallClockResponse?>(
            new()
        );

        result.IsSuccess.ShouldBeTrue();
        var restored = result.Value.ShouldNotBeNull();
        restored.LastCallChangeAt.ShouldBe(endedAt);
        restored.LastHeatingEndedAt.ShouldBe(endedAt);
        restored.LastCoolingEndedAt.ShouldBe(DateTimeOffset.MinValue);
        restored.InterruptedCall.ShouldBeNull();
    }
}
