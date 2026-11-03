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
  cO2RecirculationEnabled: boolean;
  cO2RecirculationMode: CO2RecirculationMode;
  cO2RecirculationTriggerPpm: number;
  cO2RecirculationSatisfiedPpm: number;
  cO2RecirculationDurationMs: number;
  heatingAutoBalanceEnabled: boolean;
  heatingBalanceTriggerC: number;
  heatingBalanceSatisfiedC: number;
  heatingAutoBalanceDurationMs: number;
};

export type UpdateThermostatConfigurationRequest = Pick<
  Thermostat,
  | 'hysteresisC'
  | 'cO2RecirculationEnabled'
  | 'cO2RecirculationMode'
  | 'cO2RecirculationTriggerPpm'
  | 'cO2RecirculationSatisfiedPpm'
  | 'cO2RecirculationDurationMs'
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
