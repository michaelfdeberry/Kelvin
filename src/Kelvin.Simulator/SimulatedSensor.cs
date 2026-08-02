namespace Kelvin.Simulator;

internal sealed class SimulatedSensor
{
    private static readonly Random Random = new();

    public required int SensorNumber { get; init; }

    public required byte[] MacAddress { get; init; }

    public required float TemperatureC { get; set; }

    public required float HumidityPercentage { get; set; }

    public required ushort CO2LevelPpm { get; set; }

    public required float BatteryLevelPercentage { get; set; }

    public required bool Enabled { get; set; }

    private float targetOffsetC;

    public static SimulatedSensor Create(int index, float baseTemp)
    {
        var mac = new byte[] { 0x02, 0xAA, 0x00, 0x00, 0x00, (byte)(0x10 + index) };
        var temperatureOffset = (float)(Random.NextDouble() * 4.0 - 2.0);

        return new SimulatedSensor
        {
            SensorNumber = index,
            MacAddress = mac,
            TemperatureC = baseTemp + temperatureOffset,
            HumidityPercentage = 40.0f + (float)Random.NextDouble() * 10.0f,
            CO2LevelPpm = (ushort)(700 + Random.Next(0, 150)),
            BatteryLevelPercentage = 100.0f,
            Enabled = true,
            targetOffsetC = temperatureOffset,
        };
    }

    public void Step(float baseTemp, SimulatorScenario scenario)
    {
        var drift = (float)(Random.NextDouble() * 0.2 - 0.1);
        targetOffsetC = scenario switch
        {
            SimulatorScenario.Heating => Math.Min(targetOffsetC + 0.03f, 3.0f),
            SimulatorScenario.Cooling => Math.Max(targetOffsetC - 0.03f, -3.0f),
            _ => targetOffsetC * 0.98f,
        };

        TemperatureC = baseTemp + targetOffsetC + drift;
        HumidityPercentage = Math.Clamp(
            HumidityPercentage + (float)(Random.NextDouble() * 0.3 - 0.15),
            20.0f,
            70.0f
        );
        CO2LevelPpm = (ushort)Math.Clamp(CO2LevelPpm + Random.Next(-5, 6), 400, 2500);
        BatteryLevelPercentage = Math.Max(0.0f, BatteryLevelPercentage - 0.0001f);
    }

    public override string ToString()
    {
        var mac = string.Join(":", MacAddress.Select(byteValue => byteValue.ToString("X2")));
        return $"#{SensorNumber:00} {mac} {(Enabled ? "online" : "offline")} temp={TemperatureC:F2}C humidity={HumidityPercentage:F2}% co2={CO2LevelPpm} battery={BatteryLevelPercentage:F2}%";
    }
}
