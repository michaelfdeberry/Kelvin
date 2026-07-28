import { html, LitElement } from 'lit';
import { customElement, state } from 'lit/decorators.js';
import { classMap } from 'lit/directives/class-map.js';
import { when } from 'lit/directives/when.js';

import '../../components/settings-general-tab/settings-general-tab.js';
import '../../components/settings-location-tab/settings-location-tab.js';
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

        <div
          class="settings-view__tabs"
          role="tablist"
          aria-label="Settings sections"
        >
          <button
            class=${classMap({
              'settings-view__tab-button': true,
              'settings-view__tab-button--active': this.activeTab === 'general',
            })}
            role="tab"
            aria-selected=${this.activeTab === 'general'}
            @click=${() => this.activateTab('general')}
          >
            General
          </button>
          <button
            class=${classMap({
              'settings-view__tab-button': true,
              'settings-view__tab-button--active': this.activeTab === 'location',
            })}
            role="tab"
            aria-selected=${this.activeTab === 'location'}
            @click=${() => this.activateTab('location')}
          >
            Current Location
          </button>
        </div>

        ${when(this.activeTab === 'general', () => html`<app-settings-general-tab></app-settings-general-tab>`)}
        ${when(this.activeTab === 'location', () => html`<app-settings-location-tab></app-settings-location-tab>`)}
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
