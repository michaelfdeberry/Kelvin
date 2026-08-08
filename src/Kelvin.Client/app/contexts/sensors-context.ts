import { createContext } from '@lit/context';

import { EnvironmentReading, Sensor } from '../models/sensors';

export const sensorsContext = createContext<Sensor[]>('sensors');

export const defaultSensors: Sensor[] = [];

export const environmentReadingsContext = createContext<EnvironmentReading>('environment-readings');

export const defaultEnvironmentReadings: Partial<EnvironmentReading> = {};
