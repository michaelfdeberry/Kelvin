namespace Kelvin.Simulator;

internal sealed class SimulatedHmi
{
    private static readonly Random Random = new();
    private const float InitialRoomOffsetRangeC = 0.6f;
    private const int MaxMessageSize = 2048;

    private readonly Dictionary<HmiMessageType, PendingMessage> pendingMessages = [];

    public required int HmiNumber { get; init; }

    public required byte[] MacAddress { get; init; }

    public required float TemperatureC { get; set; }

    public required float HumidityPercentage { get; set; }

    public required float BatteryLevelPercentage { get; set; }

    public required bool Enabled { get; set; }

    public required float RoomOffsetC { get; init; }

    public HmiThermostatState? ThermostatState { get; set; }

    public HmiControlStateMessage? ControlState { get; set; }

    public HmiEnvironmentReading? EnvironmentReading { get; set; }
    public HmiPreferences? Preferences { get; set; }

    public string Label =>
        $"hmi #{HmiNumber:00} {string.Join(":", MacAddress.Select(byteValue => byteValue.ToString("X2")))}";

    public static SimulatedHmi Create(int index, float baseTemp)
    {
        var mac = new byte[] { 0x02, 0xBB, 0x00, 0x00, 0x00, (byte)(0x10 + index) };
        var temperatureOffset = (float)(
            Random.NextDouble() * (InitialRoomOffsetRangeC * 2.0f) - InitialRoomOffsetRangeC
        );

        return new SimulatedHmi
        {
            HmiNumber = index,
            MacAddress = mac,
            TemperatureC = baseTemp + temperatureOffset,
            HumidityPercentage = 40.0f + (float)Random.NextDouble() * 10.0f,
            BatteryLevelPercentage = 100.0f,
            Enabled = true,
            RoomOffsetC = temperatureOffset,
        };
    }

    public void Step(float ambientTemperatureC)
    {
        TemperatureC = ambientTemperatureC + RoomOffsetC;
        HumidityPercentage = Math.Clamp(
            HumidityPercentage + (float)(Random.NextDouble() * 0.3 - 0.15),
            20.0f,
            70.0f
        );
        BatteryLevelPercentage = Math.Max(0.0f, BatteryLevelPercentage - 0.0001f);
    }

    // Mirrors the firmware's bounded per-type reassembly and strict continuation validation.
    public bool TryReassemble(
        HmiMessageType type,
        byte chunkIndex,
        byte chunkCount,
        uint messageId,
        byte[] body,
        out byte[] message
    )
    {
        message = [];

        if (
            type
            is not (
                HmiMessageType.ThermostatStateChunk
                or HmiMessageType.ControlStateChanged
                or HmiMessageType.EnvironmentReadingChanged
                or HmiMessageType.PreferencesChanged
            )
        )
            return false;

        if (chunkCount == 0 || chunkIndex >= chunkCount || body.Length > MaxMessageSize)
        {
            pendingMessages.Remove(type);
            return false;
        }

        if (chunkCount == 1)
        {
            pendingMessages.Remove(type);
            message = body;
            return true;
        }

        if (chunkIndex == 0)
        {
            pendingMessages[type] = new PendingMessage(chunkCount, messageId);
        }

        if (
            !pendingMessages.TryGetValue(type, out var pending)
            || pending.MessageId != messageId
            || pending.ChunkCount != chunkCount
            || pending.NextChunkIndex != chunkIndex
            || pending.Data.Count + body.Length > MaxMessageSize
        )
        {
            pendingMessages.Remove(type);
            return false;
        }

        pending.Data.AddRange(body);
        pending.NextChunkIndex++;

        if (pending.NextChunkIndex < chunkCount)
        {
            return false;
        }

        pendingMessages.Remove(type);
        message = [.. pending.Data];
        return true;
    }

    public override string ToString() =>
        $"{Label} {(Enabled ? "online" : "offline")} temp={TemperatureC:F2}C humidity={HumidityPercentage:F2}% battery={BatteryLevelPercentage:F2}%";

    public void DiscardPending(HmiMessageType type) => pendingMessages.Remove(type);

    private sealed class PendingMessage(byte chunkCount, uint messageId)
    {
        public uint MessageId { get; } = messageId;

        public byte ChunkCount { get; } = chunkCount;

        public int NextChunkIndex { get; set; }

        public List<byte> Data { get; } = [];
    }
}
