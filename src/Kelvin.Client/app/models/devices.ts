export type DeviceType = 'Humidifier';

export type Device = {
  id: string;
  name: string;
  type: DeviceType;
  macAddress?: string | null;
  enabled: boolean;
  lastSeenAt?: string | null;
};

export type Devices = {
  devices: Device[];
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

export type Humidifiers = {
  humidifiers: Humidifier[];
};

export type UpdateHumidifierConfigurationRequest = Pick<Humidifier, 'startMonth' | 'endMonth' | 'targets'>;
