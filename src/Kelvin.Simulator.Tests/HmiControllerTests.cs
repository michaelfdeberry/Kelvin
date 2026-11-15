using System.Buffers.Binary;
using Kelvin.Protocol;
using Shouldly;
using Xunit;

namespace Kelvin.Simulator.Tests;

public class HmiControllerTests
{
    private readonly HmiFleet fleet = new(1, 21.5f);
    private readonly List<byte[]> frames = [];
    private readonly List<string> messages = [];

    private HmiController Controller => new(fleet, frames.Add, messages.Add, (_, _) => { });
    private SimulatedHmi Hmi => fleet.ActiveHmis.Single();

    [Theory]
    [InlineData("hmi reading", (byte)HmiMessageType.SensorReading)]
    [InlineData("hmi mode heating", (byte)HmiMessageType.SetMode)]
    [InlineData("hmi fan on", (byte)HmiMessageType.SetFanEnabled)]
    [InlineData("hmi setpoint heating 23", (byte)HmiMessageType.SetSetPoint)]
    [InlineData("hmi schedule add heating 08:00 17:00 22", (byte)HmiMessageType.UpsertSchedule)]
    [InlineData("hmi lockouts none 26", (byte)HmiMessageType.SetForecastLockouts)]
    public void Execute_WritesAddressedCommand(string input, byte expectedType)
    {
        Controller.Execute((HmiDeviceCommand)SimulatorCommandParser.Parse(input));

        var frame = frames.ShouldHaveSingleItem();
        frame[2..8].ShouldBe(Hmi.MacAddress);
        frame[10..14].ShouldBe(HmiProtocol.FrameTag);
        frame[14].ShouldBe(expectedType);
        HmiProtocol
            .TryReadEnvelope(frame.AsSpan(14), out _, out _, out _, out _, out _)
            .ShouldBeTrue();
    }

    [Fact]
    public void OfflineDevice_NeitherSendsNorAppliesReplies_ButCanPrintState()
    {
        var hmi = Hmi;
        hmi.Enabled = false;

        Controller.Execute(new HmiSetFanCommand(0, true));
        Controller.Receive(hmi.MacAddress, PreferencesFrame());
        Controller.Execute(new HmiStateCommand(0));

        frames.ShouldBeEmpty();
        hmi.Preferences.ShouldBeNull();
        messages.ShouldContain("  ThermostatState: none received");
    }

    [Fact]
    public void InvalidDeviceIndex_DoesNotSend()
    {
        Controller.Execute(new HmiSetFanCommand(99, true));

        frames.ShouldBeEmpty();
        messages.ShouldContain("Invalid HMI index.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void MissingSchedule_DoesNotSend(int index)
    {
        Controller.Execute(new HmiRemoveScheduleCommand(0, index));
        Controller.Execute(
            new HmiUpsertScheduleCommand(
                0,
                index,
                HmiRunType.Heating,
                new TimeOnly(8, 0),
                new TimeOnly(17, 0),
                22f
            )
        );

        frames.ShouldBeEmpty();
        messages.Count.ShouldBe(2);
    }

    [Fact]
    public void ScheduleCommands_UseIdsFromLastReceivedState()
    {
        var id = Guid.NewGuid();
        Hmi.ThermostatState = new(
            HmiRunMode.Heating,
            false,
            0.6f,
            null,
            null,
            [],
            [new HmiSchedule(id, HmiRunType.Heating, new TimeOnly(8, 0), new TimeOnly(17, 0), 22f)]
        );

        Controller.Execute(new HmiRemoveScheduleCommand(0, 0));
        Controller.Execute(
            new HmiUpsertScheduleCommand(
                0,
                0,
                HmiRunType.Cooling,
                new TimeOnly(9, 0),
                new TimeOnly(18, 0),
                24f
            )
        );

        frames.Count.ShouldBe(2);
        foreach (var frame in frames)
        {
            HmiProtocol
                .TryReadEnvelope(frame.AsSpan(14), out _, out _, out _, out _, out var body)
                .ShouldBeTrue();
            HmiCborMap.Parse(body).Id(1).ShouldBe(id);
        }
    }

    [Fact]
    public void Receive_UpdatesPreferences_AndIgnoresMalformedOrUnknownReplies()
    {
        Controller.Receive(Hmi.MacAddress, PreferencesFrame());
        Hmi.Preferences.ShouldBe(new HmiPreferences(1, 0));
        Controller.Receive(Hmi.MacAddress, [.. HmiProtocol.FrameTag, 5]);
        Controller.Receive(new byte[6], PreferencesFrame());

        Hmi.Preferences.ShouldBe(new HmiPreferences(1, 0));
        messages.ShouldContain(message => message.Contains("malformed frame"));
        messages.ShouldContain(message => message.Contains("unknown device"));
    }

    [Fact]
    public void Receive_UpdatesControlAndEnvironmentReadings()
    {
        var control = HmiCbor.Encode(
            5,
            writer =>
            {
                writer.WriteUInt32(1);
                writer.WriteUInt32((uint)HmiControlState.Heating);
                for (uint key = 2; key <= 5; key++)
                {
                    writer.WriteUInt32(key);
                    writer.WriteSingle(20f + key);
                }
            }
        );
        var reading = HmiCbor.Encode(
            3,
            writer =>
            {
                for (uint key = 1; key <= 3; key++)
                {
                    writer.WriteUInt32(key);
                    writer.WriteSingle(20f + key);
                }
            }
        );

        Controller.Receive(
            Hmi.MacAddress,
            [
                .. HmiProtocol.FrameTag,
                .. HmiProtocol.Envelope(HmiMessageType.ControlStateChanged, control),
            ]
        );
        Controller.Receive(
            Hmi.MacAddress,
            [
                .. HmiProtocol.FrameTag,
                .. HmiProtocol.Envelope(HmiMessageType.EnvironmentReadingChanged, reading),
            ]
        );

        Hmi.ControlState.ShouldBe(
            new HmiControlStateMessage(HmiControlState.Heating, 22f, 23f, 24f, 25f)
        );
        Hmi.EnvironmentReading.ShouldBe(new HmiEnvironmentReading(21f, 22f, 23f));
    }

    [Fact]
    public void Receive_MalformedEnvelopeDiscardsPendingChunks()
    {
        var frame = PreferencesFrame();
        var body = frame[(4 + HmiProtocol.EnvelopeHeaderSize)..];
        var first = Chunk(HmiMessageType.PreferencesChanged, 0, body[..3]);
        var last = Chunk(HmiMessageType.PreferencesChanged, 1, body[3..]);
        Controller.Receive(Hmi.MacAddress, first);
        Controller.Receive(
            Hmi.MacAddress,
            [.. HmiProtocol.FrameTag, (byte)HmiMessageType.PreferencesChanged]
        );
        Controller.Receive(Hmi.MacAddress, last);

        Hmi.Preferences.ShouldBeNull();
        Controller.Receive(Hmi.MacAddress, first);
        Controller.Receive(Hmi.MacAddress, last);
        Hmi.Preferences.ShouldBe(new HmiPreferences(1, 0));
    }

    private static byte[] PreferencesFrame() =>
        [
            .. HmiProtocol.FrameTag,
            .. HmiProtocol.Envelope(
                HmiMessageType.PreferencesChanged,
                Convert.FromHexString("A3000101010200")
            ),
        ];

    private static byte[] Chunk(HmiMessageType type, byte index, byte[] body)
    {
        var envelope = HmiProtocol.Envelope(type, body);
        envelope[1] = index;
        envelope[2] = 2;
        BinaryPrimitives.WriteUInt32LittleEndian(envelope.AsSpan(5, 4), 42);
        return [.. HmiProtocol.FrameTag, .. envelope];
    }
}
