export enum TemperatureUnit {
  Celsius,
  Fahrenheit,
}

export enum TimeFormat {
  Hour24,
  Hour12,
}

export type PreferencesResponse = {
  temperatureUnit: TemperatureUnit;
  timeFormat: TimeFormat;
  locationId: number | null;
  locationName: string | null;
};
