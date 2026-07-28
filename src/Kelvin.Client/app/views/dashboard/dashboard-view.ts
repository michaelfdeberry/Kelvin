import { html, LitElement } from 'lit';
import { customElement } from 'lit/decorators.js';

import '../../components/sensor-list/sensor-list.js';
import '../../components/stats-panel/stats-panel.js';
import '../../components/thermostat-control/thermostat-control.js';
import '../../components/weather-forecast/weather-forecast.js';
import sharedStyles from '../../shared.styles.js';
import dashboardViewStyles from './dashboard-view.styles.js';

@customElement('app-dashboard-view')
export class DashboardView extends LitElement {
  static override styles = [sharedStyles, dashboardViewStyles];

  override render() {
    return html`
      <div class="dashboard-view__shell">
        <section class="dashboard-view__main">
          <app-weather-forecast></app-weather-forecast>
          <app-thermostat-control></app-thermostat-control>
          <app-sensor-list></app-sensor-list>
        </section>

        <aside class="dashboard-view__stats">
          <app-stats-panel></app-stats-panel>
        </aside>
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'app-dashboard-view': DashboardView;
  }
}
