namespace Kelvin.Simulator;

internal enum AmbientTrend
{
    Neutral,
    Warming,
    Cooling,
}

internal sealed record AmbientDirective(AmbientTrend Trend, float TargetTemperatureC);

internal sealed class AmbientSimulation(float baseTemperatureC)
{
    private const float DefaultHysteresisC = 0.6f;
    private const float SlewRateCPerMinute = 0.5f;

    public float BaseTemperatureC { get; private set; } = baseTemperatureC;
    public float TemperatureC { get; private set; } = baseTemperatureC;
    public SimulatorScenario Scenario { get; set; } = SimulatorScenario.Auto;
    public AmbientDirective LastDirective { get; private set; } = new(AmbientTrend.Neutral, 0f);

    public void SetBaseTemperature(float temperatureC)
    {
        BaseTemperatureC = temperatureC;
        TemperatureC = temperatureC;
    }

    public AmbientDirective ResolveDirective(GatewayStatus status)
    {
        if (Scenario != SimulatorScenario.Auto)
        {
            return Scenario switch
            {
                SimulatorScenario.Heating => new(AmbientTrend.Warming, BaseTemperatureC + 1.5f),
                SimulatorScenario.Cooling => new(AmbientTrend.Cooling, BaseTemperatureC - 1.5f),
                _ => new(AmbientTrend.Neutral, BaseTemperatureC),
            };
        }

        var target = status.CallContext?.TargetTemperatureC ?? BaseTemperatureC;
        var hysteresis =
            status.CallContext?.HysteresisC ?? status.Thermostat?.HysteresisC ?? DefaultHysteresisC;

        return status.Control?.CallState switch
        {
            "Heating" => new(AmbientTrend.Warming, target + hysteresis),
            "Cooling" => new(AmbientTrend.Cooling, target - hysteresis),
            _ => status.Thermostat?.Mode switch
            {
                "Cooling" => new(AmbientTrend.Warming, target + hysteresis),
                "Heating" => new(AmbientTrend.Cooling, target - hysteresis),
                _ => new(AmbientTrend.Neutral, target),
            },
        };
    }

    public void Step(AmbientDirective directive, float fleetBiasC, TimeSpan elapsed)
    {
        LastDirective = directive;
        var maxDelta = (float)(SlewRateCPerMinute * elapsed.TotalMinutes);
        if (maxDelta <= 0)
            return;

        var target = directive.TargetTemperatureC - fleetBiasC;
        TemperatureC =
            Math.Abs(target - TemperatureC) <= maxDelta
                ? target
                : TemperatureC + MathF.Sign(target - TemperatureC) * maxDelta;
    }
}
