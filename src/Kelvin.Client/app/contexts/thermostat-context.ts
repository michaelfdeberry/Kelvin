import { createContext } from '@lit/context';

import { Thermostat } from '../models/thermostat';

export const thermostatContext = createContext<Thermostat>('thermostat');

export const defaultThermostat: Thermostat = {
  mode: 'Disabled',
  fanEnabled: false,
  hysteresisC: 0.6,
};
