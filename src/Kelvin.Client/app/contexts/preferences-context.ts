import { createContext } from '@lit/context';

import { Preferences } from '../models/preferences.js';

export const preferencesContext = createContext<Preferences>('preferences');

export const defaultPreferences: Preferences = {
  temperatureUnit: 'Celsius',
  timeFormat: 'Hour24',
  locationId: null,
  locationName: null,
};
