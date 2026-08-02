namespace Kelvin.Simulator;

internal sealed record GatewayStatus(
    ThermostatSnapshot? Thermostat = null,
    ControlStateSnapshot? Control = null
);

internal sealed record ThermostatSnapshot(string Mode, bool FanEnabled, float HysteresisC);

internal sealed record ControlStateSnapshot(string ControlState, string CallState, bool FanOn);

internal abstract record SimulatorCommand;

internal sealed record SetBaseTempCommand(float TemperatureC) : SimulatorCommand;

internal sealed record AddSensorCommand : SimulatorCommand;

internal sealed record RemoveSensorCommand(int Index) : SimulatorCommand;

internal sealed record ToggleSensorCommand(int Index, bool Enabled) : SimulatorCommand;

internal sealed record ToggleAllSensorsCommand(bool Enabled) : SimulatorCommand;

internal sealed record SetScenarioCommand(SimulatorScenario Scenario) : SimulatorCommand;

internal sealed record ListSensorsCommand : SimulatorCommand;

internal sealed record StatusCommand : SimulatorCommand;

internal enum SimulatorScenario
{
    Auto,
    Idle,
    Heating,
    Cooling,
}
