using Kelvin.Server.Models;

namespace Kelvin.Server.Channels;

public interface ISensorPacketChannel : IChannelBase<SensorPacket>;

public class SensorPacketChannel(Logger<SensorPacketChannel> logger) : ChannelBase<SensorPacket>(logger), ISensorPacketChannel { };
