export type PreferencesResponse = {
  temperatureUnit: 'celsius' | 'fahrenheit';
  timeFormat: 'hour12' | 'hour24';
  locationId: number | null;
  locationName: string | null;
};
