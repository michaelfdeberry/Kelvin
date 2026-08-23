import '../sensor-card/sensor-card.js';

import { consume } from '@lit/context';
import { html, LitElement, nothing } from 'lit';
import { customElement } from 'lit/decorators.js';
import { classMap } from 'lit/directives/class-map.js';

import sensorListStyles from './sensor-list.styles.js';
import { sensorsContext } from '../../../../contexts/sensors-context.js';
import { Sensor } from '../../../../models/sensors.js';
import { isKioskMode, kioskMacAddress, normalizeMacAddress } from '../../../../services/kiosk.js';
import sharedStyles from '../../../../shared.styles.js';

@customElement('app-sensor-list')
export class SensorList extends LitElement {
  @consume({ context: sensorsContext, subscribe: true }) sensors!: Sensor[];

  static override styles = [sharedStyles, sensorListStyles];

  override render() {
    const sensors = this.getVisibleSensors();
    if (!sensors.length) return nothing;

    return html`
      <div
        class="${classMap({
          'sensor-list__cards': true,
          'sensor-list__cards--kiosk': isKioskMode(),
        })}"
        aria-label="Sensor readings"
      >
        ${sensors.map(sensor => html`<app-sensor-card .sensorId=${sensor.id}></app-sensor-card>`)}
      </div>
    `;
  }

  private getVisibleSensors(): Sensor[] {
    const enabled = this.sensors.filter(s => s.enabled);
    if (!isKioskMode()) return enabled;

    return enabled.filter(s => normalizeMacAddress(s.macAddress) === kioskMacAddress);
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-sensor-list': SensorList;
  }
}
