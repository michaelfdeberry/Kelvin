import { html, LitElement } from 'lit';
import { customElement, state } from 'lit/decorators.js';

import '../../components/settings-general/settings-general.js';
import '../../components/settings-location/settings-location.js';
import { SettingsTab } from '../../models/settings-tab.js';
import sharedStyles from '../../shared.styles.js';
import settingsViewStyles from './settings-view.styles.js';

@customElement('app-settings-view')
export class SettingsView extends LitElement {
  static override styles = [sharedStyles, settingsViewStyles];

  @state()
  private activeTab: 'general' | 'location' = 'location';

  override render() {
    return html`
      <section class="settings-view__panel">
        <h1 class="settings-view__title">Settings</h1>
        <p class="settings-view__description">Manage system preferences and location data.</p>

        <app-settings-general></app-settings-general>
        <app-settings-location></app-settings-location>
      </section>
    `;
  }

  private activateTab(tab: SettingsTab) {
    this.activeTab = tab;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'app-settings-view': SettingsView;
  }
}
