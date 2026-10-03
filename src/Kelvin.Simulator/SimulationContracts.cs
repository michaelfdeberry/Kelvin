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

internal sealed record AddHmiCommand : SimulatorCommand;

internal sealed record RemoveHmiCommand(int Index) : SimulatorCommand;

internal sealed record ToggleHmiCommand(int Index, bool Enabled) : SimulatorCommand;

internal sealed record ToggleAllHmisCommand(bool Enabled) : SimulatorCommand;

internal sealed record ListHmisCommand : SimulatorCommand;

internal sealed record HmiHelpCommand : SimulatorCommand;

internal abstract record HmiDeviceCommand(int Index) : SimulatorCommand;

internal sealed record HmiStateCommand(int Index) : HmiDeviceCommand(Index);

internal sealed record HmiSendReadingCommand(int Index) : HmiDeviceCommand(Index);

internal sealed record HmiSetModeCommand(int Index, HmiRunMode Mode) : HmiDeviceCommand(Index);

internal sealed record HmiSetFanCommand(int Index, bool Enabled) : HmiDeviceCommand(Index);

internal sealed record HmiSetSetPointCommand(int Index, HmiRunType Type, float TargetTemperatureC)
    : HmiDeviceCommand(Index);

// ScheduleIndex refers to the schedule list in the HMI's last received thermostat state; null adds a new one.
internal sealed record HmiUpsertScheduleCommand(
    int Index,
    int? ScheduleIndex,
    HmiRunType Type,
    TimeOnly Start,
    TimeOnly End,
    float TargetTemperatureC
) : HmiDeviceCommand(Index);

internal sealed record HmiRemoveScheduleCommand(int Index, int ScheduleIndex)
    : HmiDeviceCommand(Index);

internal sealed record HmiSetLockoutsCommand(
    int Index,
    float? HeatingLockoutC,
    float? CoolingLockoutC
) : HmiDeviceCommand(Index);

internal enum SimulatorScenario
{
    Auto,
    Idle,
    Heating,
    Cooling,
}
