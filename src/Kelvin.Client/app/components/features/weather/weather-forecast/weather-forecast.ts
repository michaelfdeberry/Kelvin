import '../../../shared/temperature/temperature.js';

import { consume } from '@lit/context';
import { html, LitElement, nothing } from 'lit';
import { customElement } from 'lit/decorators.js';
import { when } from 'lit/directives/when.js';

import weatherForecastStyles from './weather-forecast.styles.js';
import { WEATHER_ICONS } from './weather-icons.js';
import { defaultWeather, weatherContext } from '../../../../contexts/weather-context.js';
import { WeatherForecastResponse, WeatherForecastDay } from '../../../../models/forecast-day.js';
import sharedStyles from '../../../../shared.styles.js';

@customElement('app-weather-forecast')
export class WeatherForecast extends LitElement {
  static override styles = [sharedStyles, weatherForecastStyles];

  // The server pushes a new forecast whenever it fetches one, so there is nothing to poll for.
  @consume({ context: weatherContext, subscribe: true })
  private forecast: WeatherForecastResponse | undefined = defaultWeather;

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
            low !== undefined,
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
    if (!this.forecast) return nothing;

    const forecast = this.forecast;
    return html`
      <div
        class="weather-forecast__container"
        aria-label="3 day forecast"
      >
        ${this.renderCurrent(forecast)} ${forecast.daily.map((day, index) => this.renderForecastDay(day, index))}
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
