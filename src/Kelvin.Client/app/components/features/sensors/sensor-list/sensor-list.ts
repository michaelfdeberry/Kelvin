import '../sensor-card/sensor-card.js';

import { consume } from '@lit/context';
import { html, LitElement, nothing } from 'lit';
import { customElement } from 'lit/decorators.js';

import sensorListStyles from './sensor-list.styles.js';
import { sensorsContext } from '../../../../contexts/sensors-context.js';
import { Sensor } from '../../../../models/sensors.js';
import sharedStyles from '../../../../shared.styles.js';

@customElement('app-sensor-list')
export class SensorList extends LitElement {
  @consume({ context: sensorsContext, subscribe: true }) sensors!: Sensor[];

  static override styles = [sharedStyles, sensorListStyles];

  override render() {
    if (!this.sensors.length) return nothing;

    return html`
      <div
        class="sensor-list__cards"
        aria-label="Sensor readings"
      >
        ${this.sensors.filter(s => s.enabled).map(sensor => html`<app-sensor-card .sensorId=${sensor.id}></app-sensor-card>`)}
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'app-sensor-list': SensorList;
  }
}
