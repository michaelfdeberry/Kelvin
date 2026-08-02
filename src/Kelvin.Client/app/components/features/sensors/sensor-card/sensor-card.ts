import '../sensor-settings/sensor-settings.js';
import '../../../shared/temperature/temperature.js';

import { consume } from '@lit/context';
import { html, LitElement, nothing, TemplateResult } from 'lit';
import { customElement, property, query } from 'lit/decorators.js';
import { classMap } from 'lit/directives/class-map.js';
import { when } from 'lit/directives/when.js';

import sensorCardStyles from './sensor-card.styles.js';
import { sensorsContext, environmentReadingsContext } from '../../../../contexts/sensors-context.js';
import { Sensor, EnvironmentReading, SensorReading } from '../../../../models/sensors.js';
import sharedStyles from '../../../../shared.styles.js';
import { SensorSettings } from '../sensor-settings/sensor-settings.js';

@customElement('app-sensor-card')
export class SensorCard extends LitElement {
  static override styles = [sharedStyles, sensorCardStyles];

  @consume({ context: sensorsContext, subscribe: true })
  sensors!: Sensor[];

  @consume({ context: environmentReadingsContext, subscribe: true })
  environmentReading!: EnvironmentReading;

  @query('app-sensor-settings')
  private sensorEditor?: SensorSettings;

  @property({ type: String })
  sensorId = '';

  get sensor(): Sensor | undefined {
    return this.sensors.find(sensor => sensor.id === this.sensorId);
  }

  get reading(): SensorReading | undefined {
    return this.environmentReading?.areas?.[this.sensorId];
  }

  private renderCardContent(isUnconfigured = false): TemplateResult | typeof nothing {
    if (!this.sensor) return nothing;

    return html`
      <div class="sensor-card__title">
        ${when(
          isUnconfigured,
          () => 'Tap to Configure',
          () => this.sensor?.name ?? this.sensor?.macAddress ?? 'Unknown Sensor',
        )}
      </div>
      <div class="sensor-card__value">
        ${when(
          !!this.reading?.temperatureC,
          () => html`
            <app-temperature
              .temperature=${this.reading?.temperatureC}
              show-unit
            ></app-temperature>
          `,
          () => html`<span class="sensor-card__no-value">--</span>`,
        )}
      </div>
      <div class="sensor-card__subtitle">
        ${when(
          !!this.reading,
          () => html`
            ${when(this.sensor?.hasHumiditySensor, () => html`<div>${this.reading?.humidityPercentage.toFixed(1) ?? 0}% RH</div>`)}
            ${when(this.sensor?.hasCO2Sensor, () => html`<div>${this.reading?.cO2LevelPpm ?? 0}ppm CO₂</div>`)}
          `,
          () =>
            html`<div>--</div>
              <div>--</div>`,
        )}
      </div>
    `;
  }

  private isBatteryLow(): boolean {
    if (!this.sensor) return false;
    if (!this.sensor?.hasBattery) return false;
    if (this.reading?.batteryLevelPercentage === undefined) return false;
    if (this.reading?.batteryLevelPercentage === null) return false;
    if (this.reading?.batteryLevelPercentage >= 25) return false;
    return true;
  }

  private renderBatteryBadge(): TemplateResult | typeof nothing {
    if (!this.isBatteryLow()) return nothing;

    const batteryLevel = this.reading?.batteryLevelPercentage ?? 0;
    return html`<div class="badge badge--danger">LOW BATTERY (${batteryLevel}%)</div>`;
  }

  override render() {
    const isBatteryLow = this.isBatteryLow();
    const isUnconfigured = !!this.sensor && !this.sensor?.name;

    return when(
      isUnconfigured,
      () => html`
        <app-sensor-settings .sensorId=${this.sensorId}></app-sensor-settings>
        <button
          class="${classMap({
            'sensor-card': true,
            'sensor-card--unconfigured': isUnconfigured,
          })}"
          @click=${() => this.sensorEditor?.open()}
        >
          <div class="badge badge--warning">NEW SENSOR</div>
          ${this.renderCardContent(isUnconfigured)}
        </button>
      `,
      () => html`
        <div
          class="${classMap({
            'sensor-card': true,
            'sensor-card--unconfigured': isUnconfigured,
            'sensor-card--low-battery': isBatteryLow,
          })}"
        >
          ${when(isBatteryLow, () => this.renderBatteryBadge())} ${this.renderCardContent(isUnconfigured)}
        </div>
      `,
    );
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'app-sensor-card': SensorCard;
  }
}
