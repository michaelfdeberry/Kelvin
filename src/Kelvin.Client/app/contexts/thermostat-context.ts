import { createContext } from '@lit/context';

import { Schedule, SetPoint, Thermostat } from '../models/thermostat';

export const thermostatContext = createContext<Thermostat>('thermostat');

export const defaultThermostat: Thermostat = {
  mode: 'Disabled',
  fanEnabled: false,
  hysteresisC: 0.6,
  co2RecirculationEnabled: false,
  co2RecirculationMode: 'Average',
  co2RecirculationTriggerPpm: 1000,
  co2RecirculationSatisfiedPpm: 800,
  co2RecirculationDurationMs: 60000,
  heatingAutoBalanceEnabled: false,
  heatingBalanceTriggerC: 3,
  heatingBalanceSatisfiedC: 1.5,
  heatingAutoBalanceDurationMs: 60000,
};

export const setPointsContext = createContext<SetPoint[]>('setpoints');
export const defaultSetPoints: SetPoint[] = [];

export const schedulesContext = createContext<Schedule[]>('schedules');
export const defaultSchedules: Schedule[] = [];
