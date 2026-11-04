using Shouldly;
using Xunit;

namespace Kelvin.Simulator.Tests;

public class SimulatedHmiTests
{
    [Fact]
    public void Create_InitializesIdentityAndReadings()
    {
        var hmi = SimulatedHmi.Create(3, 21.5f);

        hmi.HmiNumber.ShouldBe(3);
        hmi.MacAddress.ShouldBe(new byte[] { 0x02, 0xBB, 0, 0, 0, 0x13 });
        hmi.Enabled.ShouldBeTrue();
        hmi.RoomOffsetC.ShouldBeInRange(-0.6f, 0.6f);
        hmi.TemperatureC.ShouldBe(21.5f + hmi.RoomOffsetC);
        hmi.HumidityPercentage.ShouldBeInRange(40f, 50f);
        hmi.BatteryLevelPercentage.ShouldBe(100f);
        hmi.ThermostatState.ShouldBeNull();
        hmi.ControlState.ShouldBeNull();
        hmi.EnvironmentReading.ShouldBeNull();
        hmi.Preferences.ShouldBeNull();
    }

    [Fact]
    public void Step_FollowsAmbientWithoutChangingRoomOffset()
    {
        var hmi = SimulatedHmi.Create(0, 21.5f);
        var offset = hmi.RoomOffsetC;

        foreach (var ambient in new[] { 15f, 30f, 21.5f })
        {
            hmi.Step(ambient);
            hmi.TemperatureC.ShouldBe(ambient + offset);
            hmi.RoomOffsetC.ShouldBe(offset);
        }

        hmi.BatteryLevelPercentage.ShouldBeLessThan(100f);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(100f, 0.00001f)]
    public void Step_ClampsHumidityAndNeverLetsBatteryGoNegative(float humidity, float battery)
    {
        var hmi = SimulatedHmi.Create(0, 21.5f);
        hmi.HumidityPercentage = humidity;
        hmi.BatteryLevelPercentage = battery;

        hmi.Step(20f);

        hmi.HumidityPercentage.ShouldBeInRange(20f, 70f);
        hmi.BatteryLevelPercentage.ShouldBe(0f);
    }

    [Fact]
    public void Fleet_AddAndRemove_PreserveRemainingIdentitiesAndDoNotReuseNumbers()
    {
        var fleet = new HmiFleet(2, 21.5f);
        fleet.TryGet(1, out var remaining).ShouldBeTrue();

        fleet.RemoveHmi(0, out var removed).ShouldBeTrue();
        removed.ShouldNotBeNull();
        removed.HmiNumber.ShouldBe(0);
        fleet.TryGet(0, out var first).ShouldBeTrue();
        first.ShouldBeSameAs(remaining);
        var added = fleet.AddHmi(25f);

        fleet.Count.ShouldBe(2);
        added.HmiNumber.ShouldBe(2);
        added.MacAddress.SequenceEqual(removed.MacAddress).ShouldBeFalse();
        added.TemperatureC.ShouldBe(25f + added.RoomOffsetC);
        fleet.FindByMacAddress(removed.MacAddress).ShouldBeNull();
        fleet.FindByMacAddress(added.MacAddress.ToArray()).ShouldBeSameAs(added);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Fleet_InvalidIndex_DoesNotMutateRoster(int index)
    {
        var fleet = new HmiFleet(2, 21.5f);

        fleet.TryGet(index, out var found).ShouldBeFalse();
        found.ShouldBeNull();
        fleet.RemoveHmi(index, out var removed).ShouldBeFalse();
        removed.ShouldBeNull();
        fleet.SetHmiEnabled(index, false, out var hmi).ShouldBeFalse();
        hmi.ShouldBeNull();
        fleet.Describe(index).ShouldBe("Invalid HMI index.");
        fleet.Count.ShouldBe(2);
        fleet.ActiveHmis.Count().ShouldBe(2);
    }

    [Fact]
    public void Fleet_EnableAndDisable_ControlActiveDevicesWithoutRemovingThem()
    {
        var fleet = new HmiFleet(2, 21.5f);

        fleet.SetHmiEnabled(0, false, out var hmi).ShouldBeTrue();
        hmi.ShouldNotBeNull();
        fleet.ActiveHmis.ShouldNotContain(hmi);
        fleet.FindByMacAddress(hmi.MacAddress.ToArray()).ShouldBeSameAs(hmi);
        fleet.Count.ShouldBe(2);
        fleet.Describe(0).ShouldContain("offline");
        fleet.SetHmiEnabled(0, true, out _).ShouldBeTrue();
        fleet.ActiveHmis.Count().ShouldBe(2);
        fleet.SetAllHmisEnabled(false).ShouldBe(2);
        fleet.ActiveHmis.ShouldBeEmpty();
        fleet.SetAllHmisEnabled(true).ShouldBe(2);
        fleet.ActiveHmis.Count().ShouldBe(2);
    }

    [Fact]
    public void Fleet_StepAll_UpdatesOfflineHmisToo()
    {
        var fleet = new HmiFleet(2, 21.5f);
        fleet.SetHmiEnabled(0, false, out var offline);
        offline.ShouldNotBeNull();

        fleet.StepAll(30f);

        offline.TemperatureC.ShouldBe(30f + offline.RoomOffsetC);
        offline.Enabled.ShouldBeFalse();
        foreach (var hmi in fleet.ActiveHmis)
            hmi.TemperatureC.ShouldBe(30f + hmi.RoomOffsetC);
    }

    [Fact]
    public void EmptyFleet_CanBePopulated()
    {
        var fleet = new HmiFleet(0, 21.5f);

        fleet.Count.ShouldBe(0);
        fleet.ActiveHmis.ShouldBeEmpty();
        fleet.SetAllHmisEnabled(false).ShouldBe(0);
        fleet.StepAll(30f);
        fleet.FindByMacAddress(new byte[6]).ShouldBeNull();
        fleet.AddHmi(25f).HmiNumber.ShouldBe(0);
        fleet.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData((byte)HmiMessageType.ThermostatStateChunk)]
    [InlineData((byte)HmiMessageType.ControlStateChanged)]
    [InlineData((byte)HmiMessageType.EnvironmentReadingChanged)]
    [InlineData((byte)HmiMessageType.PreferencesChanged)]
    [InlineData((byte)HmiMessageType.Notify)]
    [InlineData((byte)HmiMessageType.DismissNotification)]
    public void Reassembly_SupportedTypes_AcceptSingleFrames(byte type)
    {
        var hmi = SimulatedHmi.Create(0, 21.5f);

        hmi.TryReassemble((HmiMessageType)type, 0, 1, 42, [1, 2, 3], out var message)
            .ShouldBeTrue();
        message.ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void Reassembly_ConsecutiveChunks_ProduceOneMessage()
    {
        var hmi = SimulatedHmi.Create(0, 21.5f);

        hmi.TryReassemble(HmiMessageType.Notify, 0, 3, 42, [1, 2], out var first).ShouldBeFalse();
        first.ShouldBeEmpty();
        hmi.TryReassemble(HmiMessageType.Notify, 1, 3, 42, [3], out var second).ShouldBeFalse();
        second.ShouldBeEmpty();
        hmi.TryReassemble(HmiMessageType.Notify, 2, 3, 42, [4, 5], out var message).ShouldBeTrue();
        message.ShouldBe(new byte[] { 1, 2, 3, 4, 5 });
        hmi.TryReassemble(HmiMessageType.Notify, 2, 3, 42, [4, 5], out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0, 0, 42)]
    [InlineData(3, 3, 42)]
    [InlineData(2, 3, 42)]
    [InlineData(1, 2, 42)]
    [InlineData(1, 3, 43)]
    public void Reassembly_InvalidContinuation_DiscardsPendingMessage(
        int index,
        int count,
        int messageId
    )
    {
        var hmi = SimulatedHmi.Create(0, 21.5f);
        hmi.TryReassemble(HmiMessageType.Notify, 0, 3, 42, [1], out _);

        hmi.TryReassemble(
                HmiMessageType.Notify,
                (byte)index,
                (byte)count,
                (uint)messageId,
                [2],
                out _
            )
            .ShouldBeFalse();
        hmi.TryReassemble(HmiMessageType.Notify, 1, 3, 42, [3], out _).ShouldBeFalse();
    }

    [Fact]
    public void Reassembly_InterleavedTypes_KeepSeparatePendingMessages()
    {
        var hmi = SimulatedHmi.Create(0, 21.5f);
        hmi.TryReassemble(HmiMessageType.Notify, 0, 2, 42, [1], out _).ShouldBeFalse();
        hmi.TryReassemble(HmiMessageType.ThermostatStateChunk, 0, 2, 43, [2], out _)
            .ShouldBeFalse();

        hmi.TryReassemble(HmiMessageType.Notify, 1, 2, 42, [3], out var notification)
            .ShouldBeTrue();
        notification.ShouldBe(new byte[] { 1, 3 });
        hmi.TryReassemble(HmiMessageType.ThermostatStateChunk, 1, 2, 43, [4], out var thermostat)
            .ShouldBeTrue();
        thermostat.ShouldBe(new byte[] { 2, 4 });
    }

    [Fact]
    public void Reassembly_NewFirstChunk_ReplacesPreviousMessage()
    {
        var hmi = SimulatedHmi.Create(0, 21.5f);
        hmi.TryReassemble(HmiMessageType.Notify, 0, 2, 42, [1], out _);
        hmi.TryReassemble(HmiMessageType.Notify, 0, 2, 43, [2], out _);

        hmi.TryReassemble(HmiMessageType.Notify, 1, 2, 43, [3], out var message).ShouldBeTrue();
        message.ShouldBe(new byte[] { 2, 3 });
    }

    [Fact]
    public void Reassembly_DiscardPending_RejectsSubsequentContinuation()
    {
        var hmi = SimulatedHmi.Create(0, 21.5f);
        hmi.TryReassemble(HmiMessageType.Notify, 0, 2, 42, [1], out _);

        hmi.DiscardPending(HmiMessageType.Notify);

        hmi.TryReassemble(HmiMessageType.Notify, 1, 2, 42, [2], out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(2048, true)]
    [InlineData(2049, false)]
    public void Reassembly_EnforcesSingleFrameSizeLimit(int length, bool accepted)
    {
        var hmi = SimulatedHmi.Create(0, 21.5f);

        hmi.TryReassemble(HmiMessageType.Notify, 0, 1, 42, new byte[length], out var message)
            .ShouldBe(accepted);
        message.Length.ShouldBe(accepted ? length : 0);
    }

    [Theory]
    [InlineData(1024, true)]
    [InlineData(1025, false)]
    public void Reassembly_EnforcesCombinedSizeLimit(int secondLength, bool accepted)
    {
        var hmi = SimulatedHmi.Create(0, 21.5f);
        hmi.TryReassemble(HmiMessageType.Notify, 0, 2, 42, new byte[1024], out _);

        hmi.TryReassemble(HmiMessageType.Notify, 1, 2, 42, new byte[secondLength], out var message)
            .ShouldBe(accepted);
        message.Length.ShouldBe(accepted ? 2048 : 0);
        hmi.TryReassemble(HmiMessageType.Notify, 1, 2, 42, [1], out _).ShouldBeFalse();
    }

    [Fact]
    public void Reassembly_UnsupportedType_IsRejected()
    {
        var hmi = SimulatedHmi.Create(0, 21.5f);

        hmi.TryReassemble((HmiMessageType)255, 0, 1, 42, [1], out var message).ShouldBeFalse();
        message.ShouldBeEmpty();
    }
}
