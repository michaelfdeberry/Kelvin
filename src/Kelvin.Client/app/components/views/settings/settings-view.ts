import '../../../components/features/settings/settings-general/settings-general.js';
import '../../../components/features/settings/settings-location/settings-location.js';
import '../../../components/features/settings/settings-sensors/settings-sensors.js';

import { html, LitElement } from 'lit';
import { customElement } from 'lit/decorators.js';

import settingsViewStyles from './settings-view.styles.js';
import sharedStyles from '../../../shared.styles.js';

@customElement('app-settings-view')
export class SettingsView extends LitElement {
  static override styles = [sharedStyles, settingsViewStyles];

  override render() {
    return html`
      <section class="settings-view__panel">
        <h1 class="settings-view__title">Settings</h1>
        <p class="settings-view__description">Manage system preferences and location data.</p>

        <app-settings-general></app-settings-general>
        <app-settings-location></app-settings-location>
        <app-settings-sensors></app-settings-sensors>
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
