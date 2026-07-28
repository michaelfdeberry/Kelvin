import { TemperatureUnit } from '../models/preferences-response';

export async function formatTemperatureUnit(temperature: number, unit: TemperatureUnit): Promise<string> {
  switch (unit) {
    case TemperatureUnit.Celsius: {
      return `${temperature.toFixed(1)} °C`;
    }
    case TemperatureUnit.Fahrenheit: {
      const fahrenheitTemperature = (temperature * 9) / 5 + 32;
      return `${fahrenheitTemperature.toFixed(1)} °F`;
    }
    default:
      throw new Error(`Unknown temperature unit: ${unit}`);
  }
}
