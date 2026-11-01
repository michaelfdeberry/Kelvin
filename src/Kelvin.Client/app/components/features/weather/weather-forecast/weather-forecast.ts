import '../../../shared/temperature/temperature.js';

import { Task } from '@lit/task';
import { html, LitElement } from 'lit';
import { customElement } from 'lit/decorators.js';
import { when } from 'lit/directives/when.js';

import weatherForecastStyles from './weather-forecast.styles.js';
import { WEATHER_ICONS } from './weather-icons.js';
import { WeatherForecastResponse, WeatherForecastDay } from '../../../../models/forecast-day.js';
import resources from '../../../../services/api-resources.js';
import { apiGet } from '../../../../services/api.js';
import sharedStyles from '../../../../shared.styles.js';

const WEATHER_UPDATE_INTERVAL_MS = 30 * 60 * 1000; // 30 minutes

@customElement('app-weather-forecast')
export class WeatherForecast extends LitElement {
  static override styles = [sharedStyles, weatherForecastStyles];

  private intervalId: ReturnType<typeof setInterval> | undefined;

  override connectedCallback() {
    super.connectedCallback();

    this.intervalId = setInterval(() => this.getWeatherTask.run(), WEATHER_UPDATE_INTERVAL_MS);
  }

  override disconnectedCallback() {
    super.disconnectedCallback();

    if (this.intervalId) {
      clearInterval(this.intervalId);
    }
  }

  private getWeatherTask = new Task(this, {
    task: async (_, { signal }) => {
      return apiGet<WeatherForecastResponse>(resources.weather.getWeatherForecast, { signal });
    },
    args: () => [this],
  });

  private renderItem(summary: string, label: string, high: number, low?: number) {
    return html`
      <div
        class="weather-forecast__day"
        title="${summary}"
      >
        <span class="weather-forecast__day-label">${label}</span>
        <span
          class="weather-forecast__icon"
          aria-hidden="true"
        >
          ${WEATHER_ICONS[summary] ?? WEATHER_ICONS['Unknown']}
        </span>
        <span class="weather-forecast__temps">
          <app-temperature
            .temperature=${high}
            show-unit
          ></app-temperature>
          ${when(
            low,
            () => html`
              <span class="weather-forecast__temps-low">
                <app-temperature .temperature=${low}></app-temperature>
              </span>
            `,
          )}
        </span>
      </div>
    `;
  }

  private renderForecastDay(day: WeatherForecastDay, index: number) {
    const [year, month, dayOfMonth] = day.date.split('-').map(Number);
    const date = new Date(year!, month! - 1, dayOfMonth);
    const dateName = date.toLocaleDateString(undefined, { weekday: 'short' });
    const dateLabel = index === 0 ? 'Today' : index === 1 ? 'Tomorrow' : dateName;
    return this.renderItem(day.summary, dateLabel, day.temperatureMaxC, day.temperatureMinC);
  }

  private renderCurrent(forecast: WeatherForecastResponse) {
    const { summary, temperatureC } = forecast.current;
    return this.renderItem(summary, 'Now', temperatureC);
  }

  override render() {
    return html`
      <div
        class="weather-forecast__container"
        aria-label="3 day forecast"
      >
        ${this.getWeatherTask.render({
          pending: () => html`<p>Loading weather forecast...</p>`,
          complete: (forecast: WeatherForecastResponse) => html`
            ${this.renderCurrent(forecast)} ${forecast.daily.map((day, index) => this.renderForecastDay(day, index))}
          `,
          error: () => html`<p class="error">Failed to retrieve forecast</p>`,
        })}
      </div>
    `;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-weather-forecast': WeatherForecast;
  }
}
