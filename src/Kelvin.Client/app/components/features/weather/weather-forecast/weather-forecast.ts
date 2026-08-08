import '../../../shared/temperature/temperature.js';

import { Task } from '@lit/task';
import { html, LitElement } from 'lit';
import { customElement } from 'lit/decorators.js';

import weatherForecastStyles from './weather-forecast.styles.js';
import { WEATHER_ICONS } from './weather-icons.js';
import { WeatherForecastResponse, WeatherForecastDay } from '../../../../models/forecast-day.js';
import resources from '../../../../services/api-resources.js';
import { apiGet } from '../../../../services/api.js';
import sharedStyles from '../../../../shared.styles.js';

@customElement('app-weather-forecast')
export class WeatherForecast extends LitElement {
  static override styles = [sharedStyles, weatherForecastStyles];

  private getWeatherTask = new Task(this, {
    task: async (_, { signal }) => {
      return apiGet<WeatherForecastResponse>(resources.weather.getWeatherForecast, { signal });
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
          <app-temperature
            .temperature=${day.temperatureMaxC}
            show-unit
          ></app-temperature>
          <span class="weather-forecast__temps-low">
            <app-temperature .temperature=${day.temperatureMinC}></app-temperature>
          </span>
        </span>
      </div>
    `;
  }

  private renderCurrent(forecast: WeatherForecastResponse) {
    const current = forecast.current;

    return html`
      <div
        class="weather-forecast__day"
        title="${current.summary}"
      >
        <span class="weather-forecast__day-label">Now</span>
        <span
          class="weather-forecast__icon"
          aria-hidden="true"
        >
          ${WEATHER_ICONS[current.summary] ?? WEATHER_ICONS['Unknown']}
        </span>
        <span class="weather-forecast__temps">
          <app-temperature
            .temperature=${current.temperatureC}
            show-unit
          ></app-temperature>
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
          ${this.renderCurrent(forecast)} ${forecast.daily.map((day, index) => this.renderForecastDay(day, index))}
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
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-weather-forecast': WeatherForecast;
  }
}
