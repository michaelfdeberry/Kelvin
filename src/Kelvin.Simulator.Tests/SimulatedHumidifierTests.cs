using Shouldly;
using Xunit;

namespace Kelvin.Simulator.Tests;

public class SimulatedHumidifierTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly ControlStateSnapshot Heating = new("Enable", "Heating", true, null);

    [Theory]
    [InlineData("Enable", "Dwell")]
    [InlineData("Enable", "Cooling")]
    [InlineData("Disable", "Heating")]
    public void NotHeatingUnderKelvinControl_DoesNotPing(string control, string call)
    {
        var humidifier = new SimulatedHumidifier();

        humidifier.Tick(new ControlStateSnapshot(control, call, false, null), Now).ShouldBeNull();
        humidifier.Powered.ShouldBeFalse();
        humidifier.RelayOn.ShouldBeFalse();
    }

    [Fact]
    public void UnknownControl_DoesNotPing()
    {
        new SimulatedHumidifier().Tick(null, Now).ShouldBeNull();
    }

    [Fact]
    public void Heating_PingsImmediatelyAndEveryFifteenSeconds()
    {
        var humidifier = new SimulatedHumidifier();

        AssertPing(humidifier.Tick(Heating, Now), false);
        humidifier.Tick(Heating, Now.AddSeconds(14)).ShouldBeNull();
        AssertPing(humidifier.Tick(Heating, Now.AddSeconds(15)), false);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ServerState_IsReportedInNextPing(bool enabled)
    {
        var humidifier = new SimulatedHumidifier();
        humidifier.Tick(Heating, Now);

        humidifier.ReceiveState(State(enabled), Now.AddSeconds(1)).ShouldBeTrue();
        humidifier.RelayOn.ShouldBe(enabled);
        AssertPing(humidifier.Tick(Heating, Now.AddSeconds(15)), enabled);
    }

    [Fact]
    public void HeatingStops_ResetsRelayAndPingsImmediatelyOnRestart()
    {
        var humidifier = new SimulatedHumidifier();
        humidifier.Tick(Heating, Now);
        humidifier.ReceiveState(State(true), Now);

        humidifier.Tick(Heating with { CallState = "Dwell" }, Now.AddSeconds(1)).ShouldBeNull();
        humidifier.RelayOn.ShouldBeFalse();
        humidifier.ReceiveState(State(true), Now.AddSeconds(2)).ShouldBeFalse();
        AssertPing(humidifier.Tick(Heating, Now.AddSeconds(3)), false);
    }

    [Fact]
    public void RepliesTimeOut_ResetsRelayAfterTwoMinutes()
    {
        var humidifier = new SimulatedHumidifier();
        humidifier.Tick(Heating, Now);
        humidifier.ReceiveState(State(true), Now);

        AssertPing(humidifier.Tick(Heating, Now.AddSeconds(119)), true);
        humidifier.Tick(Heating, Now.AddSeconds(120)).ShouldBeNull();
        humidifier.RelayOn.ShouldBeFalse();
        AssertPing(humidifier.Tick(Heating, Now.AddSeconds(134)), false);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("4B48554D02A2000201F5", false)]
    [InlineData("4B48554D02A10001", false)]
    [InlineData("4B48554D01A2000101F5", false)]
    [InlineData("4B48554D02A2000101F5", true)]
    public void StateValidation_RejectsMalformedReplies(string hex, bool accepted)
    {
        var humidifier = new SimulatedHumidifier();
        humidifier.Tick(Heating, Now);

        humidifier.ReceiveState(Convert.FromHexString(hex), Now).ShouldBe(accepted);
        humidifier.RelayOn.ShouldBe(accepted);
    }

    [Theory]
    [InlineData(new string[] { }, true)]
    [InlineData(new[] { "--no-humidifier" }, false)]
    [InlineData(new[] { "--humidifier" }, true)]
    [InlineData(new[] { "--no-humidifier", "--humidifier" }, true)]
    public void Options_ControlHumidifierPresence(string[] args, bool enabled)
    {
        SimulatorOptions.Parse(["--port=COM5", .. args]).HumidifierEnabled.ShouldBe(enabled);
    }

    private static byte[] State(bool enabled) =>
        Convert.FromHexString(enabled ? "4B48554D02A2000101F5" : "4B48554D02A2000101F4");

    private static void AssertPing(byte[]? payload, bool expectedRelayOn)
    {
        payload.ShouldNotBeNull();
        Convert.ToHexString(payload).ShouldBe(
            expectedRelayOn ? "4B48554D01A2000101F5" : "4B48554D01A2000101F4"
        );
    }
}