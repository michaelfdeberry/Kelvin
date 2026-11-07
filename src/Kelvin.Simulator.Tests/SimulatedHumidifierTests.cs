using Kelvin.Protocol;
using Shouldly;
using Xunit;

namespace Kelvin.Simulator.Tests;

public class SimulatedHumidifierTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void ChecksInImmediatelyAndEveryFiveMinutes()
    {
        var humidifier = new SimulatedHumidifier();

        AssertRelayState(humidifier.Tick(Now), HumidifierMessageType.CheckIn, false);
        humidifier.Tick(Now.AddMinutes(4.9)).ShouldBeNull();
        AssertRelayState(humidifier.Tick(Now.AddMinutes(5)), HumidifierMessageType.CheckIn, false);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void State_IsAppliedAndAcknowledged(bool enabled)
    {
        var humidifier = new SimulatedHumidifier();

        var ack = humidifier.ReceiveState(State(enabled), Now);

        humidifier.RelayOn.ShouldBe(enabled);
        AssertRelayState(ack, HumidifierMessageType.StateAck, enabled);
    }

    [Fact]
    public void StatesStop_ReleasesRelayAfterTwoMinutes()
    {
        var humidifier = new SimulatedHumidifier();
        humidifier.Tick(Now);
        humidifier.ReceiveState(State(true), Now);

        humidifier.Tick(Now.AddSeconds(119));
        humidifier.RelayOn.ShouldBeTrue();
        humidifier.Tick(Now.AddSeconds(120));
        humidifier.RelayOn.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("4B48554D02A2000101F5")]
    [InlineData("4B48554D010001050000000000A2000101F5")]
    [InlineData("4B48554D020002050000000000A2000101F5")]
    [InlineData("4B48554D020001020000000000FF00")]
    [InlineData("4B484D49020001050000000000A2000101F5")]
    public void MalformedStates_AreIgnored(string hex)
    {
        var humidifier = new SimulatedHumidifier();

        humidifier.ReceiveState(Convert.FromHexString(hex), Now).ShouldBeNull();
        humidifier.RelayOn.ShouldBeFalse();
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
        Convert.FromHexString(
            enabled
                ? "4B48554D020001050000000000A2000101F5"
                : "4B48554D020001050000000000A2000101F4"
        );

    private static void AssertRelayState(
        byte[]? frame,
        HumidifierMessageType expectedType,
        bool expectedRelayOn
    )
    {
        frame.ShouldNotBeNull();
        frame.Take(4).ShouldBe(SimulatedHumidifier.FrameTag);
        DeviceEnvelope
            .TryDecode(frame.AsSpan(4), out var type, out _, out var count, out _, out var body)
            .ShouldBeTrue();
        type.ShouldBe((byte)expectedType);
        count.ShouldBe((byte)1);
        Convert.ToHexString(body).ShouldBe(expectedRelayOn ? "A2000101F5" : "A2000101F4");
    }
}
