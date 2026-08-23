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

    // Each sensor's room offset is fixed for its lifetime, so the fleet's average reading the
    // server sees is permanently skewed from the shared ambient value by this amount.
    public float AverageActiveRoomOffsetC
    {
        get
        {
            var activeSensors = ActiveSensors.ToList();
            return activeSensors.Count == 0
                ? 0f
                : activeSensors.Average(sensor => sensor.RoomOffsetC);
        }
    }

    public void StepAll(float ambientTemperatureC)
    {
        foreach (var sensor in sensors)
        {
            sensor.Step(ambientTemperatureC);
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
