export type TemperatureUnit = 'Celsius' | 'Fahrenheit';

export type TimeFormat = 'Hour24' | 'Hour12';

export type Preferences = {
  temperatureUnit: TemperatureUnit;
  timeFormat: TimeFormat;
  locationId: number | null;
  locationName: string | null;
};
