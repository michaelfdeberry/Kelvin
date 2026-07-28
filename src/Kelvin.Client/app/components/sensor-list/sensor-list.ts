import { html, LitElement } from 'lit';
import { customElement } from 'lit/decorators.js';

import type { SensorReading } from '../../models/sensor-reading.js';
import sharedStyles from '../../shared.styles.js';
import '../sensor-card/sensor-card.js';
import sensorListStyles from './sensor-list.styles.js';

const sensorReadings: SensorReading[] = [
  { title: 'Living Room (Master)', value: '69.5°F', subtitle: '42% RH • 580 ppm CO2' },
  { title: 'Bedroom', value: '68.2°F', subtitle: '45% RH • SHT40' },
  { title: 'Office', value: '69.8°F', subtitle: '40% RH • SHT40' },
];

@customElement('app-sensor-list')
export class SensorList extends LitElement {
  static override styles = [sharedStyles, sensorListStyles];

  override render() {
    return html`
      <div
        class="sensor-list__nodes"
        aria-label="Sensor readings"
      >
        ${sensorReadings.map(
          reading => html`
            <app-sensor-card
              .title=${reading.title}
              .value=${reading.value}
              .subtitle=${reading.subtitle}
            ></app-sensor-card>
          `,
        )}
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'app-sensor-list': SensorList;
  }
}
