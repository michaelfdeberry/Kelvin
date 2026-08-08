export type RunMode = 'Disabled' | 'Off' | 'Heating' | 'Cooling' | 'Automatic';

export type RunType = 'Heating' | 'Cooling';

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
};

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
};
