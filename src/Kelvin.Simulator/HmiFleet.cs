using System.Diagnostics.CodeAnalysis;

namespace Kelvin.Simulator;

internal sealed class HmiFleet
{
    private readonly List<SimulatedHmi> hmis = [];
    private int nextHmiNumber;

    public HmiFleet(int initialCount, float baseTemperatureC)
    {
        for (var index = 0; index < initialCount; index++)
        {
            AddHmi(baseTemperatureC);
        }
    }

    public int Count => hmis.Count;

    public IEnumerable<SimulatedHmi> ActiveHmis => hmis.Where(hmi => hmi.Enabled);

    public void StepAll(float ambientTemperatureC)
    {
        foreach (var hmi in hmis)
        {
            hmi.Step(ambientTemperatureC);
        }
    }

    public SimulatedHmi AddHmi(float baseTemperatureC)
    {
        var hmi = SimulatedHmi.Create(nextHmiNumber++, baseTemperatureC);
        hmis.Add(hmi);
        return hmi;
    }

    public bool TryGet(int index, [NotNullWhen(true)] out SimulatedHmi? hmi)
    {
        hmi = index >= 0 && index < hmis.Count ? hmis[index] : null;
        return hmi is not null;
    }

    public SimulatedHmi? FindByMacAddress(byte[] macAddress) =>
        hmis.FirstOrDefault(hmi => hmi.MacAddress.AsSpan().SequenceEqual(macAddress));

    public bool RemoveHmi(int index, out SimulatedHmi? removedHmi)
    {
        if (!TryGet(index, out removedHmi))
        {
            return false;
        }

        hmis.RemoveAt(index);
        return true;
    }

    public bool SetHmiEnabled(int index, bool enabled, out SimulatedHmi? hmi)
    {
        if (!TryGet(index, out hmi))
        {
            return false;
        }

        hmi.Enabled = enabled;
        return true;
    }

    public int SetAllHmisEnabled(bool enabled)
    {
        foreach (var hmi in hmis)
        {
            hmi.Enabled = enabled;
        }

        return hmis.Count;
    }

    public string Describe(int index) =>
        TryGet(index, out var hmi) ? $"[{index}] {hmi}" : "Invalid HMI index.";
}
