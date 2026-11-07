using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Features.Humidifiers;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Humidifiers;

public class HumidifierConfigurationTests : IDisposable
{
    private readonly KelvinContextHarness _harness = new();
    private readonly IDispatcher _dispatcher = A.Fake<IDispatcher>();

    public void Dispose() => _harness.Dispose();

    private async Task<Guid> SeedAsync()
    {
        await using var context = _harness.CreateContext();
        var device = new HumidifierDevice
        {
            Name = "Humidifier",
            Configuration = new HumidifierConfiguration(),
        };
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device.Id;
    }

    [Fact]
    public async Task Get_ReturnsConfigurationWithSortedTargets()
    {
        await SeedAsync();
        await using var context = _harness.CreateContext();

        var result = await new GetHumidifiersHandler(context).HandleAsync(
            new GetHumidifiersRequest()
        );

        var humidifier = result.Value!.Humidifiers.ShouldHaveSingleItem();
        humidifier.StartMonth.ShouldBe(10);
        humidifier.EndMonth.ShouldBe(3);
        humidifier
            .Targets.Select(t => t.OutdoorTemperatureC)
            .ShouldBe(
                HumidifierConfiguration.DefaultTargets().Select(t => t.OutdoorTemperatureC).Order()
            );
    }

    [Fact]
    public async Task Update_PersistsConfigurationAndNotifies()
    {
        var id = await SeedAsync();
        await using var context = _harness.CreateContext();

        var result = await new UpdateHumidifierConfigurationHandler(
            context,
            _dispatcher
        ).HandleAsync(
            new UpdateHumidifierConfigurationRequest(
                id,
                new HumidifierConfigurationRequest(11, 2, [new(0, 40), new(-10, 30)])
            )
        );

        result.IsSuccess.ShouldBeTrue();
        var saved = (
            (HumidifierDevice)(await _harness.CreateContext().Devices.FindAsync(id))!
        ).Configuration;
        saved.StartMonth.ShouldBe(11);
        saved.EndMonth.ShouldBe(2);
        saved.Targets.Select(t => t.OutdoorTemperatureC).ShouldBe([-10f, 0f]);
        A.CallTo(() => _dispatcher.PublishAsync(A<DevicesChangedEvent>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(10, 13)]
    public async Task Update_RejectsInvalidMonths(int start, int end)
    {
        var id = await SeedAsync();
        await using var context = _harness.CreateContext();

        var result = await new UpdateHumidifierConfigurationHandler(
            context,
            _dispatcher
        ).HandleAsync(
            new UpdateHumidifierConfigurationRequest(
                id,
                new HumidifierConfigurationRequest(start, end, [])
            )
        );

        result.Error.ShouldBe(UpdateHumidifierConfigurationErrors.InvalidMonth);
    }

    [Fact]
    public async Task Update_RejectsDuplicateOrOutOfRangeTargets()
    {
        var id = await SeedAsync();
        await using var context = _harness.CreateContext();
        var handler = new UpdateHumidifierConfigurationHandler(context, _dispatcher);

        (await handler.HandleAsync(new(id, new(10, 3, [new(0, 40), new(0, 30)])))).Error.ShouldBe(
            UpdateHumidifierConfigurationErrors.InvalidTargets
        );
        (await handler.HandleAsync(new(id, new(10, 3, [new(0, 140)])))).Error.ShouldBe(
            UpdateHumidifierConfigurationErrors.InvalidTargets
        );
    }

    [Fact]
    public async Task Update_UnknownHumidifier_ReturnsNotFound()
    {
        await using var context = _harness.CreateContext();

        var result = await new UpdateHumidifierConfigurationHandler(
            context,
            _dispatcher
        ).HandleAsync(new(Guid.NewGuid(), new(10, 3, [])));

        result.Error.ShouldBe(UpdateHumidifierConfigurationErrors.NotFoundError);
    }
}
