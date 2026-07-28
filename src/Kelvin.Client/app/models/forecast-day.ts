export type WeatherCurrent = {
  timestamp: string;
  temperatureC: number;
  apparentTemperatureC: number;
  humidity: number;
  windSpeedKph: number;
  weatherCode: number;
  summary: string;
}

export type WeatherForecastResponse = {
  latitude: number;
  longitude: number;
  timezone: string;
  retrievedAt: string;
  current: WeatherCurrent;
  daily: WeatherForecastDay[];
}

export type WeatherForecastDay = {
  date: string;
  icon: string;
  temperatureMinC: string;
  temperatureMaxC: string;
  weatherCode: number;
  summary: string;
};