import { RunMode } from './thermostat';

export type ControlState = 'Disable' | 'Enable' | 'Dwell' | 'Heating' | 'Cooling' | 'FanOn' | 'FanOff' | 'Startup' | 'Fault';

export type ControlChangeKind = 'Control' | 'Call' | 'Fan' | 'Lifecycle';

export type ControlStateChange = {
  id: string;
  kind: ControlChangeKind;
  state: ControlState;
  previousState?: ControlState;
  changedAt: string;
  previousStateDurationSeconds?: number;
  reason?: string;
  environmentTemperatureC?: number;
  humidityPercentage?: number;
  targetTemperatureC?: number;
  hysteresisC?: number;
  forecastTemperatureC?: number;
  mode?: RunMode;
  scheduleId?: string;
  setPointId?: string;
};
