import { createContext } from '@lit/context';

import type { WeatherForecastResponse } from '../models/forecast-day.js';

// undefined until the server has a forecast to push, e.g. while no location is configured
export const weatherContext = createContext<WeatherForecastResponse | undefined>('weather-forecast');

export const defaultWeather: WeatherForecastResponse | undefined = undefined;
