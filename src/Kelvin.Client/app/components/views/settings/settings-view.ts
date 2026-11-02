import '../../../components/features/settings/settings-general/settings-general.js';
import '../../../components/features/settings/settings-location/settings-location.js';
import '../../../components/features/settings/settings-sensors/settings-sensors.js';
import '../../../components/features/settings/settings-thermostat/settings-thermostat.js';
import '../../../components/features/settings/settings-gateway/settings-gateway.js';

import { html, LitElement, TemplateResult } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import { classMap } from 'lit/directives/class-map.js';

import settingsViewStyles from './settings-view.styles.js';
import sharedStyles from '../../../shared.styles.js';

type SettingsSection = {
  id: string;
  label: string;
  description: string;
  render: () => TemplateResult;
};

const sections: SettingsSection[] = [
  {
    id: 'general',
    label: 'General',
    description: 'Display preferences and your location.',
    render: () => html`
      <app-settings-general></app-settings-general>
      <app-settings-location></app-settings-location>
    `,
  },
  {
    id: 'climate',
    label: 'Climate',
    description: 'Thermostat behavior, hysteresis, CO2 recirculation and heating balance.',
    render: () => html`<app-settings-thermostat></app-settings-thermostat>`,
  },
  {
    id: 'devices',
    label: 'Devices',
    description: 'Sensors and the gateway wiring.',
    render: () => html`
      <app-settings-sensors></app-settings-sensors>
      <app-settings-gateway></app-settings-gateway>
    `,
  },
];

@customElement('app-settings-view')
export class SettingsView extends LitElement {
  static override styles = [sharedStyles, settingsViewStyles];

  @property()
  section?: string;

  private get activeSection(): SettingsSection {
    return sections.find(section => section.id === this.section) ?? sections[0]!;
  }

  override render() {
    const active = this.activeSection;

    return html`
      <section class="settings-view__panel">
        <h1 class="settings-view__title">Settings</h1>
        <p class="settings-view__description">${active.description}</p>
        <div class="settings-view__layout">
          <nav
            class="settings-view__nav"
            aria-label="Settings sections"
          >
            ${sections.map(
              section => html`
                <a
                  class=${classMap({ 'settings-view__link': true, 'settings-view__link--active': section === active })}
                  href="/settings/${section.id}"
                  aria-current=${section === active ? 'page' : 'false'}
                >
                  ${section.label}
                </a>
              `,
            )}
          </nav>
          <div class="settings-view__content">${active.render()}</div>
        </div>
      </section>
    `;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-settings-view': SettingsView;
  }
}
