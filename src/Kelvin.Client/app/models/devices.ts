export type DeviceType = 'Humidifier';

export type Device = {
  id: string;
  name: string;
  type: DeviceType;
  macAddress?: string | null;
  enabled: boolean;
  lastSeenAt?: string | null;
};

// Everything the server pushes about devices; local updates may carry only the parts that changed.
export type DevicesSettings = {
  devices: Device[];
  humidifiers: Humidifier[];
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
