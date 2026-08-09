import '../../../components/shared/alert/alert.js';
import '../../../components/shared/modal/modal.js';
import '../../../components/features/sensors/sensor-list/sensor-list.js';
import '../../../components/features/analytics/stats-panel/stats-panel.js';
import '../../../components/features/thermostat/thermostat-control/thermostat-control.js';
import '../../../components/features/weather/weather-forecast/weather-forecast.js';

import { html, LitElement } from 'lit';
import { customElement } from 'lit/decorators.js';

import dashboardViewStyles from './dashboard-view.styles.js';
import sharedStyles from '../../../shared.styles.js';

@customElement('app-dashboard-view')
export class DashboardView extends LitElement {
  static override styles = [sharedStyles, dashboardViewStyles];

  override connectedCallback() {
    super.connectedCallback();
    document.title = 'Kelvin - Dashboard';
  }

  override render() {
    return html`
      <div class="dashboard-view__shell">
        <section class="dashboard-view__main">
          <app-weather-forecast></app-weather-forecast>
          <app-thermostat-control></app-thermostat-control>
          <app-sensor-list></app-sensor-list>
          <a
            href="/settings"
            class="dashboard-view__settings-button button button--secondary"
          >
            Settings
          </a>
        </section>

        <aside class="dashboard-view__stats">
          <app-stats-panel></app-stats-panel>
        </aside>
      </div>
    `;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-dashboard-view': DashboardView;
  }
}
