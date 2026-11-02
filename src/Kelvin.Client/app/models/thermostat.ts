export type RunMode = 'Disabled' | 'Off' | 'Heating' | 'Cooling' | 'Automatic';

export type RunType = 'Heating' | 'Cooling';

export type CO2RecirculationMode = 'Average' | 'Peak';

export type SetPoint = {
  id: string;
  type: RunType;
  targetTemperatureC: number;
};

export type Schedule = {
  id: string;
  type: RunType;
  startTime: string;
  endTime: string;
  targetTemperatureC: number;
};

export type SetPointsResponse = {
  setPoints: SetPoint[];
};

export type SchedulesResponse = {
  schedules: Schedule[];
};

export type Thermostat = {
  id?: string;
  mode: RunMode;
  fanEnabled: boolean;
  hysteresisC: number;
  heatingLockoutC?: number;
  coolingLockoutC?: number;
  co2RecirculationEnabled: boolean;
  co2RecirculationMode: CO2RecirculationMode;
  co2RecirculationTriggerPpm: number;
  co2RecirculationSatisfiedPpm: number;
  co2RecirculationDurationMs: number;
  heatingAutoBalanceEnabled: boolean;
  heatingBalanceTriggerC: number;
  heatingBalanceSatisfiedC: number;
  heatingAutoBalanceDurationMs: number;
};

export type UpdateThermostatConfigurationRequest = Pick<
  Thermostat,
  | 'hysteresisC'
  | 'co2RecirculationEnabled'
  | 'co2RecirculationMode'
  | 'co2RecirculationTriggerPpm'
  | 'co2RecirculationSatisfiedPpm'
  | 'co2RecirculationDurationMs'
  | 'heatingAutoBalanceEnabled'
  | 'heatingBalanceTriggerC'
  | 'heatingBalanceSatisfiedC'
  | 'heatingAutoBalanceDurationMs'
>;

export type SetPointInput = {
  id?: string;
  type: RunType;
  targetTemperatureC: number;
};

export type ScheduleInput = {
  id?: string;
  type: RunType;
  startTime: string;
  endTime: string;
  targetTemperatureC: number;
};

export type UpdateThermostatSettingsRequest = {
  heatingLockoutC?: number;
  coolingLockoutC?: number;
  setPoints: SetPointInput[];
  schedules: ScheduleInput[];
  targetMode?: RunMode;
};
