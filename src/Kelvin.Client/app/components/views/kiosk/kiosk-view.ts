import '../../../components/shared/alert/alert.js';
import '../../../components/shared/modal/modal.js';
import '../../../components/features/sensors/sensor-list/sensor-list.js';
import '../../../components/features/thermostat/thermostat-control/thermostat-control.js';
import '../../../components/features/weather/weather-forecast/weather-forecast.js';

import { html, LitElement } from 'lit';
import { customElement } from 'lit/decorators.js';
import { unsafeSVG } from 'lit/directives/unsafe-svg.js';

import kioskViewStyles from './kiosk-view.styles.js';
import refreshIcon from '../../../../assets/icons/refresh.svg?raw';
import sharedStyles from '../../../shared.styles.js';

@customElement('app-kiosk-view')
export class KioskView extends LitElement {
  static override styles = [sharedStyles, kioskViewStyles];

  override connectedCallback() {
    super.connectedCallback();
    document.title = 'Kelvin - Kiosk';
  }

  private handleRefresh(): void {
    window.location.reload();
  }

  override render() {
    return html`
      <div class="kiosk-view__shell">
        <button
          class="kiosk-view__refresh-button button button--icon"
          aria-label="Refresh"
          @click=${this.handleRefresh}
        >
          ${unsafeSVG(refreshIcon)}
        </button>

        <section class="kiosk-view__main">
          <app-thermostat-control></app-thermostat-control>
          <app-sensor-list></app-sensor-list>
        </section>

        <aside class="kiosk-view__weather">
          <app-weather-forecast></app-weather-forecast>
        </aside>
      </div>
    `;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-kiosk-view': KioskView;
  }
}
