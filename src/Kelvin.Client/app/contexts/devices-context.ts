import { createContext } from '@lit/context';

import { Device, DeviceStates, Humidifier } from '../models/devices';

export const devicesContext = createContext<Device[]>('devices');

export const defaultDevices: Device[] = [];

export const humidifiersContext = createContext<Humidifier[]>('humidifiers');

export const defaultHumidifiers: Humidifier[] = [];

export const deviceStatesContext = createContext<DeviceStates>('device-states');

export const defaultDeviceStates: DeviceStates = {};
