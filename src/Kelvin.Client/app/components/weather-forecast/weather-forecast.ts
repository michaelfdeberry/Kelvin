import { Task } from '@lit/task';
import { html, LitElement } from 'lit';
import { customElement } from 'lit/decorators.js';

import type { WeatherForecastDay, WeatherForecastResponse } from '../../models/forecast-day.js';
import sharedStyles from '../../shared.styles.js';
import weatherForecastStyles from './weather-forecast.styles.js';
import { WEATHER_ICONS } from './weather-icons.js';

@customElement('app-weather-forecast')
export class WeatherForecast extends LitElement {
  static override styles = [sharedStyles, weatherForecastStyles];

  private getWeatherTask = new Task(this, {
    task: async (_, { signal }) => {
      const response = await fetch('/api/weather/forecast', { signal });
      const result = await response.json();
      if (!response.ok) {
        if (result.message) {
          throw new Error(result.message);
        }
        throw new Error('Failed to fetch weather forecast');
      }
      return result as Promise<WeatherForecastResponse>;
    },
    args: () => [this],
  });

  private renderForecastDay(day: WeatherForecastDay, index: number) {
    const [year, month, dayOfMonth] = day.date.split('-').map(Number);
    const date = new Date(year!, month! - 1, dayOfMonth);
    const dateName = date.toLocaleDateString(undefined, { weekday: 'short' });

    return html`
      <div
        class="weather-forecast__day"
        title="${day.summary}"
      >
        <span class="weather-forecast__day-label">${index === 0 ? 'Today' : dateName}</span>
        <span
          class="weather-forecast__icon"
          aria-hidden="true"
        >
          ${WEATHER_ICONS[day.summary] ?? WEATHER_ICONS['Unknown']}
        </span>
        <span class="weather-forecast__temps">
          ${day.temperatureMaxC}&deg;<span class="weather-forecast__temps-low">${day.temperatureMinC}&deg;</span>
        </span>
      </div>
    `;
  }

  override render() {
    return this.getWeatherTask.render({
      pending: () => html`<p>Loading weather forecast...</p>`,
      complete: (forecast: WeatherForecastResponse) => html`
        <div
          class="weather-forecast__container"
          aria-label="5 day forecast"
        >
          ${forecast.daily.map((day, index) => this.renderForecastDay(day, index))}
        </div>
      `,
      error: error => {
        const message = error instanceof Error ? error.message : 'Unknown error';
        return html`<p>${message}</p>`;
      },
    });
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'app-weather-forecast': WeatherForecast;
  }
}
