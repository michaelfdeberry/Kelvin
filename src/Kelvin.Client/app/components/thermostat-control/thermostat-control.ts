import { html, LitElement } from 'lit';
import { customElement } from 'lit/decorators.js';

import type { ThermostatMode } from '../../models/thermostat-mode.js';
import sharedStyles from '../../shared.styles.js';
import thermostatControlStyles from './thermostat-control.styles.js';

const controlModes: ThermostatMode[] = [
  { label: 'Auto', active: true },
  { label: 'Heat', active: false },
  { label: 'Cool', active: false },
  { label: 'Off', active: false },
];

@customElement('app-thermostat-control')
export class ThermostatControl extends LitElement {
  static override styles = [sharedStyles, thermostatControlStyles];

  override render() {
    return html`
      <div
        class="thermostat-control__dial-container"
        aria-label="Thermostat control"
      >
        <div class="thermostat-control__dial-inner">
          <p class="thermostat-control__target-temp">Set to 70°F</p>
          <h2 class="thermostat-control__current-temp">69.1°</h2>
          <div class="thermostat-control__status-badge">🔥 HEATING</div>
        </div>
      </div>

      <div
        class="thermostat-control__controls"
        role="group"
        aria-label="Thermostat mode"
      >
        ${controlModes.map(
          mode => html`
            <button
              class=${this.getButtonClass(mode.active)}
              type="button"
            >
              ${mode.label}
            </button>
          `,
        )}
      </div>
    `;
  }

  private getButtonClass(active: boolean): string {
    return active ? 'thermostat-control__button thermostat-control__button--active' : 'thermostat-control__button';
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'app-thermostat-control': ThermostatControl;
  }
}
