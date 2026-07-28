import { html, LitElement } from 'lit';
import { customElement } from 'lit/decorators.js';

import sharedStyles from '../../shared.styles.js';
import statsPanelStyles from './stats-panel.styles.js';

const statSections = [
  {
    title: 'System Telemetry',
    rows: [
      ['Aggregate Average:', '69.16°F'],
      ['Weather Forecast:', '34°F'],
      ['ControlService:', 'Active'],
    ],
  },
  {
    title: 'Hysteresis Logic',
    rows: [
      ['Dead Band:', '±0.5°C (0.9°F)'],
      ['Heating Trigger:', '≤ 69.1°F'],
      ['Heating Satisfied:', '≥ 70.9°F'],
    ],
  },
  {
    title: 'Hardware Relays (GPIO)',
    rows: [
      ['R1 (Heat - W):', 'LOW (ON)'],
      ['R2 (Cool - Y):', 'HIGH (OFF)'],
      ['R3 (Fan - G):', 'HIGH (OFF)'],
      ['R4 (Gateway Control):', 'LOW (ARMED)'],
    ],
  },
];

@customElement('app-stats-panel')
export class StatsPanel extends LitElement {
  static override styles = [sharedStyles, statsPanelStyles];

  override render() {
    return html`
      ${statSections.map(
        section => html`
          <section class="stats-panel__section">
            <h3 class="stats-panel__section-title">${section.title}</h3>
            ${section.rows.map(
              ([label, value]) => html`
                <div class="stats-panel__row">
                  <span>${label}</span>
                  <span class="stats-panel__value">
                    ${
                      label === 'ControlService:'
                        ? html`<span
                              class="stats-panel__status-dot"
                              aria-hidden="true"
                            ></span
                            >${value}`
                        : value
                    }
                  </span>
                </div>
              `,
            )}
          </section>
        `,
      )}
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'app-stats-panel': StatsPanel;
  }
}
