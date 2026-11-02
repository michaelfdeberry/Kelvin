using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Thermostat;

public class UpdateThermostatConfigurationTests
{
    private static UpdateThermostatConfigurationRequest ValidRequest(
        float hysteresisC = 0.8f,
        ushort recirculationTrigger = 1200,
        ushort recirculationSatisfied = 900,
        int recirculationDurationMs = 120000,
        float balanceTriggerC = 4f,
        float balanceSatisfiedC = 2f,
        int balanceDurationMs = 90000
    ) =>
        new(
            hysteresisC,
            true,
            CO2RecirculationMode.Peak,
            recirculationTrigger,
            recirculationSatisfied,
            recirculationDurationMs,
            true,
            balanceTriggerC,
            balanceSatisfiedC,
            balanceDurationMs
        );

    private static async Task SeedThermostatAsync(KelvinContextHarness harness)
    {
        await using var context = harness.CreateContext();
        context.Thermostats.Add(new Models.Thermostat { Mode = RunMode.Heating });
        await context.SaveChangesAsync();
    }

    private static IDispatcher CreateDispatcher(Result? safetyResult = null)
    {
        var dispatcher = A.Fake<IDispatcher>();
        A.CallTo(() =>
                dispatcher.DispatchAsync(
                    A<ValidateThermostatSafetyRequest>._,
                    A<CancellationToken>._
                )
            )
            .Returns(safetyResult ?? Result.Success());
        return dispatcher;
    }

    [Fact]
    public async Task NoThermostat_ReturnsFailure()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();

        var result = await new UpdateThermostatConfigurationHandler(
            context,
            new MemoryCache(new MemoryCacheOptions()),
            CreateDispatcher()
        ).HandleAsync(ValidRequest());

        result.Error.ShouldBe(UpdateThermostatConfigurationErrors.ThermostatNotFound);
    }

    [Fact]
    public async Task ValidRequest_PersistsEveryValue_ClearsCache_AndPublishes()
    {
        using var harness = new KelvinContextHarness();
        await SeedThermostatAsync(harness);
        await using var context = harness.CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set(ThermostatCache.Key, new object());
        var dispatcher = CreateDispatcher();

        var result = await new UpdateThermostatConfigurationHandler(
            context,
            cache,
            dispatcher
        ).HandleAsync(ValidRequest());

        result.IsSuccess.ShouldBeTrue();
        cache.TryGetValue(ThermostatCache.Key, out _).ShouldBeFalse();
        A.CallTo(() =>
                dispatcher.PublishAsync(A<ThermostatConfigChangedEvent>._, A<CancellationToken>._)
            )
            .MustHaveHappenedOnceExactly();

        await using var verifyContext = harness.CreateContext();
        var thermostat = verifyContext.Thermostats.Single();
        thermostat.HysteresisC.ShouldBe(0.8f);
        thermostat.CO2RecirculationEnabled.ShouldBeTrue();
        thermostat.CO2RecirculationMode.ShouldBe(CO2RecirculationMode.Peak);
        thermostat.CO2RecirculationTriggerPpm.ShouldBe((ushort)1200);
        thermostat.CO2RecirculationSatisfiedPpm.ShouldBe((ushort)900);
        thermostat.CO2RecirculationDurationMs.ShouldBe(120000);
        thermostat.HeatingAutoBalanceEnabled.ShouldBeTrue();
        thermostat.HeatingBalanceTriggerC.ShouldBe(4f);
        thermostat.HeatingBalanceSatisfiedC.ShouldBe(2f);
        thermostat.HeatingAutoBalanceDurationMs.ShouldBe(90000);
    }

    [Theory]
    [InlineData(0.2f)]
    [InlineData(2.1f)]
    public async Task HysteresisOutOfRange_ReturnsFailure(float hysteresisC)
    {
        using var harness = new KelvinContextHarness();
        await SeedThermostatAsync(harness);
        await using var context = harness.CreateContext();

        var result = await new UpdateThermostatConfigurationHandler(
            context,
            new MemoryCache(new MemoryCacheOptions()),
            CreateDispatcher()
        ).HandleAsync(ValidRequest(hysteresisC: hysteresisC));

        result.Error.ShouldBe(UpdateThermostatConfigurationErrors.InvalidHysteresis);
    }

    [Theory]
    [InlineData(1000, 1000, 60000)]
    [InlineData(1000, 1200, 60000)]
    [InlineData(1000, 800, 0)]
    public async Task InvalidRecirculation_ReturnsFailure(
        int trigger,
        int satisfied,
        int durationMs
    )
    {
        using var harness = new KelvinContextHarness();
        await SeedThermostatAsync(harness);
        await using var context = harness.CreateContext();

        var result = await new UpdateThermostatConfigurationHandler(
            context,
            new MemoryCache(new MemoryCacheOptions()),
            CreateDispatcher()
        ).HandleAsync(
            ValidRequest(
                recirculationTrigger: (ushort)trigger,
                recirculationSatisfied: (ushort)satisfied,
                recirculationDurationMs: durationMs
            )
        );

        result.Error.ShouldBe(UpdateThermostatConfigurationErrors.InvalidRecirculation);
    }

    [Theory]
    [InlineData(3f, 3f, 60000)]
    [InlineData(3f, 4f, 60000)]
    [InlineData(3f, -1f, 60000)]
    [InlineData(3f, 1f, 0)]
    public async Task InvalidBalancing_ReturnsFailure(
        float triggerC,
        float satisfiedC,
        int durationMs
    )
    {
        using var harness = new KelvinContextHarness();
        await SeedThermostatAsync(harness);
        await using var context = harness.CreateContext();

        var result = await new UpdateThermostatConfigurationHandler(
            context,
            new MemoryCache(new MemoryCacheOptions()),
            CreateDispatcher()
        ).HandleAsync(
            ValidRequest(
                balanceTriggerC: triggerC,
                balanceSatisfiedC: satisfiedC,
                balanceDurationMs: durationMs
            )
        );

        result.Error.ShouldBe(UpdateThermostatConfigurationErrors.InvalidBalancing);
    }

    [Fact]
    public async Task UnsafeHysteresisForExistingTargets_ReturnsFailure_AndPersistsNothing()
    {
        using var harness = new KelvinContextHarness();
        await SeedThermostatAsync(harness);
        await using var context = harness.CreateContext();
        var dispatcher = CreateDispatcher(
            Result.Failure(ValidateThermostatSafetyErrors.UnsafeTargetOverlap)
        );

        var result = await new UpdateThermostatConfigurationHandler(
            context,
            new MemoryCache(new MemoryCacheOptions()),
            dispatcher
        ).HandleAsync(ValidRequest());

        result.Error.ShouldBe(ValidateThermostatSafetyErrors.UnsafeTargetOverlap);

        await using var verifyContext = harness.CreateContext();
        verifyContext.Thermostats.Single().HysteresisC.ShouldBe(0.6f);
        A.CallTo(() =>
                dispatcher.PublishAsync(A<ThermostatConfigChangedEvent>._, A<CancellationToken>._)
            )
            .MustNotHaveHappened();
    }
}
