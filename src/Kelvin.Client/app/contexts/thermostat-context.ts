import { createContext } from '@lit/context';

import { Schedule, SetPoint, Thermostat } from '../models/thermostat';

export const thermostatContext = createContext<Thermostat>('thermostat');

export const defaultThermostat: Thermostat = {
  mode: 'Disabled',
  fanEnabled: false,
  hysteresisC: 0.6,
  cO2RecirculationEnabled: false,
  cO2RecirculationTriggerPpm: 1000,
  cO2RecirculationSatisfiedPpm: 800,
  cO2RecirculationDurationMs: 60000,
  heatingAutoBalanceEnabled: false,
  heatingBalanceTriggerC: 3,
  heatingBalanceSatisfiedC: 1.5,
  heatingAutoBalanceDurationMs: 60000,
};

export const setPointsContext = createContext<SetPoint[]>('setpoints');
export const defaultSetPoints: SetPoint[] = [];

export const schedulesContext = createContext<Schedule[]>('schedules');
export const defaultSchedules: Schedule[] = [];
