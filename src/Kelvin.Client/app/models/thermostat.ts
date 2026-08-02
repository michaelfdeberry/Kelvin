export type RunMode = 'Disabled' | 'Off' | 'Heating' | 'Cooling' | 'Automatic';

export type RunType = 'Heating' | 'Cooling';

export type SetPoint = {
  id: string;
  type: RunType;
  targetTemperatureC: number;
  activationTemperatureC: number | null;
};

export type Schedule = {
  id: string;
  type: RunType;
  enabled: boolean;
  startTime: string;
  endTime: string;
  targetTemperatureC: number;
  activationTemperatureC: number | null;
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
};
