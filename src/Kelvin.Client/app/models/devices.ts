export type DeviceType = 'Humidifier';

export type Device = {
  id: string;
  name: string;
  type: DeviceType;
  macAddress?: string | null;
  enabled: boolean;
  lastSeenAt?: string | null;
};

// Everything the server pushes about devices when a client connects.
export type DevicesSettings = {
  devices: Device[];
  humidifiers: Humidifier[];
};

// One created or changed device. The device and its per-type configuration are separate lists on the client, so either may be absent
// when only one of them changed.
export type DeviceChange = {
  device?: Device;
  humidifier?: Humidifier;
};

export type HumidifierState = {
  enabled: boolean;
};

// Latest reported state per device type; each type adds its own optional entry.
export type DeviceStates = {
  humidifier?: HumidifierState;
};

export type UpdateDeviceRequest = {
  name: string;
};

export type HumidityTarget = {
  outdoorTemperatureC: number;
  humidityPercentage: number;
};

export type Humidifier = {
  id: string;
  name: string;
  enabled: boolean;
  startMonth: number;
  endMonth: number;
  targets: HumidityTarget[];
};

export type UpdateHumidifierConfigurationRequest = Pick<Humidifier, 'startMonth' | 'endMonth' | 'targets'>;
