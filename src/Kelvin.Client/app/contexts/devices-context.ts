import { createContext } from '@lit/context';

import { Device, DeviceStates } from '../models/devices';

export const devicesContext = createContext<Device[]>('devices');

export const defaultDevices: Device[] = [];

export const deviceStatesContext = createContext<DeviceStates>('device-states');

export const defaultDeviceStates: DeviceStates = {};
