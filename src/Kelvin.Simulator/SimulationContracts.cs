namespace Kelvin.Simulator;

internal sealed record GatewayStatus(
    ThermostatSnapshot? Thermostat = null,
    ControlStateSnapshot? Control = null,
    ControlStateChangeSnapshot? CallContext = null
);

internal sealed record ThermostatSnapshot(string Mode, bool FanEnabled, float HysteresisC);

internal sealed record ControlStateSnapshot(
    string ControlState,
    string CallState,
    bool FanOn,
    ControlStateChangeSnapshot? LastChange
);

internal sealed record ControlStateChangeSnapshot(float? TargetTemperatureC, float? HysteresisC);

// The /api/control/state LastChange can be a Fan/Control-kind row with no target/hysteresis, so
// the call's own target/hysteresis is fetched separately via /api/control/history?kind=Call.
internal sealed record ControlHistoryPageSnapshot(IReadOnlyList<ControlStateChangeSnapshot>? Items);

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
