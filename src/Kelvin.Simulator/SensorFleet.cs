namespace Kelvin.Simulator;

internal sealed class SensorFleet
{
    private readonly List<SimulatedSensor> sensors = [];
    private int nextSensorNumber;

    public SensorFleet(int initialCount, float baseTemperatureC)
    {
        for (var index = 0; index < initialCount; index++)
        {
            AddSensor(baseTemperatureC);
        }
    }

    public int Count => sensors.Count;

    public IEnumerable<SimulatedSensor> ActiveSensors => sensors.Where(sensor => sensor.Enabled);

    public IReadOnlyList<SimulatedSensor> Sensors => sensors;

    public void StepAll(float baseTemperatureC, SimulatorScenario scenario)
    {
        foreach (var sensor in sensors)
        {
            sensor.Step(baseTemperatureC, scenario);
        }
    }

    public SimulatedSensor AddSensor(float baseTemperatureC)
    {
        var sensor = SimulatedSensor.Create(nextSensorNumber++, baseTemperatureC);
        sensors.Add(sensor);
        return sensor;
    }

    public bool RemoveSensor(int index, out SimulatedSensor? removedSensor)
    {
        if (index < 0 || index >= sensors.Count)
        {
            removedSensor = null;
            return false;
        }

        removedSensor = sensors[index];
        sensors.RemoveAt(index);
        return true;
    }

    public bool SetSensorEnabled(int index, bool enabled, out SimulatedSensor? sensor)
    {
        if (index < 0 || index >= sensors.Count)
        {
            sensor = null;
            return false;
        }

        sensor = sensors[index];
        sensor.Enabled = enabled;
        return true;
    }

    public int SetAllSensorsEnabled(bool enabled)
    {
        foreach (var sensor in sensors)
        {
            sensor.Enabled = enabled;
        }

        return sensors.Count;
    }

    public string Describe(int index)
    {
        if (index < 0 || index >= sensors.Count)
        {
            return "Invalid sensor index.";
        }

        return $"[{index}] {sensors[index]}";
    }
}
