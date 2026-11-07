import { createContext } from '@lit/context';

import { Device } from '../models/devices';

export const devicesContext = createContext<Device[]>('devices');

export const defaultDevices: Device[] = [];
