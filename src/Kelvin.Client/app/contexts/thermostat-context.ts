import { createContext } from '@lit/context';

import { Schedule, SetPoint, Thermostat } from '../models/thermostat';

export const thermostatContext = createContext<Thermostat>('thermostat');

export const defaultThermostat: Thermostat = {
  mode: 'Disabled',
  fanEnabled: false,
  hysteresisC: 0.6,
};

export const setPointsContext = createContext<SetPoint[]>('setpoints');
export const defaultSetPoints: SetPoint[] = [];

export const schedulesContext = createContext<Schedule[]>('schedules');
export const defaultSchedules: Schedule[] = [];
