export type Sensors = {
  sensors: Sensor[];
};

export type Sensor = {
  id: string;
  name: string;
  macAddress: string;
  hasBattery: boolean;
  hasCO2Sensor: boolean;
  hasHumiditySensor: boolean;
  enabled: boolean;
};

export type EnvironmentReading = {
  timestamp: string;
  temperatureC: number;
  humidityPercentage: number;
  cO2LevelPpm: number;
  areas: Record<string, SensorReading>;
};

export type SensorReading = {
  sensorId: string;
  temperatureC: number;
  humidityPercentage: number;
  cO2LevelPpm: number;
  batteryLevelPercentage?: number;
  createdAt: string;
  updatedAt: string;
};
