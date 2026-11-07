using FakeItEasy;
using Kelvin.Server.Application;
using Kelvin.Server.Features.Devices;
using Kelvin.Server.Messaging;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Devices;

public class DeviceTests : IDisposable
{
    private readonly KelvinContextHarness _harness = new();
    private readonly IDispatcher _dispatcher = A.Fake<IDispatcher>();

    public void Dispose() => _harness.Dispose();

    private async Task<Guid> SeedAsync(bool enabled = true)
    {
        await using var context = _harness.CreateContext();
        var device = new HumidifierDevice
        {
            Name = "Humidifier",
            MacAddress = "aa:bb:cc:dd:ee:ff",
            Enabled = enabled,
        };
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device.Id;
    }

    private void MustHavePublished() =>
        A.CallTo(() => _dispatcher.PublishAsync(A<DevicesChangedEvent>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

    [Fact]
    public async Task GetDevices_ExcludesDeletedDevices()
    {
        var keptId = await SeedAsync();
        var deletedId = await SeedAsync();
        await using (var context = _harness.CreateContext())
        {
            context.Devices.Remove(await context.Devices.SingleAsync(d => d.Id == deletedId));
            await context.SaveChangesAsync();
        }

        await using var readContext = _harness.CreateContext();
        var result = await new GetDevicesHandler(readContext).HandleAsync(new GetDevicesRequest());

        result.Value!.Devices.Select(d => d.Id).ShouldBe([keptId]);
        result.Value.Devices[0].Type.ShouldBe(DeviceType.Humidifier);
    }

    [Fact]
    public async Task DisableThenEnable_TogglesAndNotifies()
    {
        var id = await SeedAsync();

        await using (var context = _harness.CreateContext())
        {
            (
                await new DisableDeviceHandler(context, _dispatcher).HandleAsync(
                    new DisableDeviceRequest(id)
                )
            ).IsSuccess.ShouldBeTrue();
        }
        (await _harness.CreateContext().Devices.FindAsync(id))!.Enabled.ShouldBeFalse();

        await using (var context = _harness.CreateContext())
        {
            (
                await new EnableDeviceHandler(context, _dispatcher).HandleAsync(
                    new EnableDeviceRequest(id)
                )
            ).IsSuccess.ShouldBeTrue();
        }
        (await _harness.CreateContext().Devices.FindAsync(id))!.Enabled.ShouldBeTrue();
        A.CallTo(() => _dispatcher.PublishAsync(A<DevicesChangedEvent>._, A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task Enable_UnknownDevice_ReturnsNotFound()
    {
        await using var context = _harness.CreateContext();

        var result = await new EnableDeviceHandler(context, _dispatcher).HandleAsync(
            new EnableDeviceRequest(Guid.NewGuid())
        );

        result.Error.ShouldBe(EnableDeviceErrors.NotFoundError);
    }

    [Fact]
    public async Task DeleteThenRestore_RoundTrips()
    {
        var id = await SeedAsync();

        await using (var context = _harness.CreateContext())
        {
            (
                await new DeleteDeviceHandler(context, _dispatcher).HandleAsync(
                    new DeleteDeviceRequest(id)
                )
            ).IsSuccess.ShouldBeTrue();
        }
        (await _harness.CreateContext().Devices.FindAsync(id))!.DeletedAt.ShouldNotBeNull();

        await using (var context = _harness.CreateContext())
        {
            (
                await new RestoreDeviceHandler(context, _dispatcher).HandleAsync(
                    new RestoreDeviceRequest(id)
                )
            ).IsSuccess.ShouldBeTrue();
        }
        (await _harness.CreateContext().Devices.FindAsync(id))!.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Update_ChangesNameOnly()
    {
        var id = await SeedAsync();
        await using var context = _harness.CreateContext();

        var result = await new UpdateDeviceHandler(context, _dispatcher).HandleAsync(
            new UpdateDeviceRequest(id, new DeviceRequest("  Basement  "))
        );

        result.IsSuccess.ShouldBeTrue();
        var device = (await _harness.CreateContext().Devices.FindAsync(id))!;
        device.Name.ShouldBe("Basement");
        device.MacAddress.ShouldBe("aa:bb:cc:dd:ee:ff");
        MustHavePublished();
    }

    [Fact]
    public async Task Update_RejectsBlankName()
    {
        var id = await SeedAsync();
        await using var context = _harness.CreateContext();

        var result = await new UpdateDeviceHandler(context, _dispatcher).HandleAsync(
            new UpdateDeviceRequest(id, new DeviceRequest(" "))
        );

        result.Error.ShouldBe(UpdateDeviceErrors.NameRequired);
    }
}
