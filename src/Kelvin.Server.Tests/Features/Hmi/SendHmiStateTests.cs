using Kelvin.Server.Channels;
using Kelvin.Server.Features.Hmi;
using Kelvin.Server.Features.Preferences;
using Kelvin.Server.Features.Thermostat;
using Kelvin.Server.Models;
using Kelvin.Server.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Kelvin.Server.Tests.Features.Panels;

public class SendHmiStateTests
{
    [Fact]
    public async Task EmptyDatabase_ReturnsAllSnapshotPartsWithIdleControlState()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var outbound = new HmiOutboundChannel();
        var handler = new SendHmiStateHandler(
            context,
            outbound,
            NullLogger<SendHmiStateHandler>.Instance,
            new GetThermostatHandler(context, cache),
            new GetPreferencesHandler(context)
        );

        var result = await handler.HandleAsync(new SendHmiStateRequest("AA:BB:CC:DD:EE:FF"));

        result.IsSuccess.ShouldBeTrue();
        var frames = Drain(outbound);
        frames
            .Select(frame => (HmiMessageType)frame[0])
            .ShouldBe(
                new[]
                {
                    HmiMessageType.ControlStateChanged,
                    HmiMessageType.PreferencesChanged,
                    HmiMessageType.ThermostatStateChunk,
                }
            );
        Convert
            .ToHexString(frames[0][HmiEnvelope.HeaderSize..])
            .ShouldBe("A60001010202F603F604F605F6");
        context.Thermostats.Count().ShouldBe(1);
        context.Preferences.Count().ShouldBe(1);
    }

    [Fact]
    public async Task Request_ReturnsLatestNonDeletedCallAndCompleteThermostatSettings()
    {
        using var harness = new KelvinContextHarness();
        await using var context = harness.CreateContext();
        var thermostat = new Models.Thermostat { Mode = RunMode.Heating, FanEnabled = true };
        thermostat.SetPoints.Add(new SetPoint { Type = RunType.Heating, TargetTemperatureC = 22f });
        context.Thermostats.Add(thermostat);
        var call = new ControlStateChange
        {
            Kind = ControlChangeKind.Call,
            State = ControlState.Heating,
            EnvironmentTemperatureC = 20f,
            TargetTemperatureC = 22f,
        };
        context.ControlStateChanges.Add(call);
        await context.SaveChangesAsync();
        context.ControlStateChanges.Add(
            new ControlStateChange { Kind = ControlChangeKind.Fan, State = ControlState.FanOn }
        );
        var deletedCall = new ControlStateChange
        {
            Kind = ControlChangeKind.Call,
            State = ControlState.Cooling,
        };
        context.ControlStateChanges.Add(deletedCall);
        await context.SaveChangesAsync();
        context.ControlStateChanges.Remove(deletedCall);
        await context.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var outbound = new HmiOutboundChannel();
        var handler = new SendHmiStateHandler(
            context,
            outbound,
            NullLogger<SendHmiStateHandler>.Instance,
            new GetThermostatHandler(context, cache),
            new GetPreferencesHandler(context)
        );

        await handler.HandleAsync(new SendHmiStateRequest("AA:BB:CC:DD:EE:FF"));

        var frames = Drain(outbound);
        frames[0][0].ShouldBe((byte)HmiMessageType.ControlStateChanged);
        frames[0][HmiEnvelope.HeaderSize..].ShouldBe(HmiControlStateEncoder.Encode(call));
        frames
            .Where(frame => frame[0] == (byte)HmiMessageType.ThermostatStateChunk)
            .SelectMany(frame => frame.Skip(HmiEnvelope.HeaderSize))
            .ShouldBe(HmiThermostatStateEncoder.Encode(thermostat));
    }

    private static List<byte[]> Drain(HmiOutboundChannel outbound)
    {
        var frames = new List<byte[]>();
        while (outbound.TryRead(out var message))
        {
            message.MacAddress.ShouldBe("AA:BB:CC:DD:EE:FF");
            frames.Add(message.Payload[FrameTags.Size..]);
        }
        return frames;
    }
}
