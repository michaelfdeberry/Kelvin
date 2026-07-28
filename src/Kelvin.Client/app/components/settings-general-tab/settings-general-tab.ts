import { Task } from '@lit/task';
import { html, LitElement } from 'lit';
import { customElement } from 'lit/decorators.js';

import { PreferencesResponse } from '../../models/preferences-response.js';
import sharedStyles from '../../shared.styles.js';
import settingsGeneralTabStyles from './settings-general-tab.styles.js';

@customElement('app-settings-general-tab')
export class SettingsGeneralTab extends LitElement {
  static override styles = [sharedStyles, settingsGeneralTabStyles];

  private preferencesTask = new Task(this, {
    task: async (_, { signal }) => {
      const response = await fetch('/api/preferences', { signal });
      const result = await response.json();

      if (!response.ok) {
        if (result.message) {
          throw new Error(result.message);
        }

        throw new Error('Failed to load preferences');
      }

      return result as PreferencesResponse;
    },
    args: () => [],
  });

  override render() {
    return this.preferencesTask.render({
      pending: () => html`
        <section
          class="settings-general-tab"
          role="tabpanel"
          aria-label="General settings"
        >
          <article class="settings-general-tab__card">
            <h2 class="settings-general-tab__title">Preferences</h2>
            <p class="settings-general-tab__hint">Loading preferences...</p>
          </article>
        </section>
      `,
      complete: preferences => html`
        <section
          class="settings-general-tab"
          role="tabpanel"
          aria-label="General settings"
        >
          <article class="settings-general-tab__card">
            <h2 class="settings-general-tab__title">Preferences</h2>
            <p class="settings-general-tab__meta">Temperature unit: <strong>${preferences.temperatureUnit}</strong></p>
            <p class="settings-general-tab__meta">Time format: <strong>${preferences.timeFormat}</strong></p>
            <p class="settings-general-tab__hint">Additional settings can be added here as backend preferences are expanded.</p>
          </article>
        </section>
      `,
      error: error => {
        const message = error instanceof Error ? error.message : 'Unknown error';
        return html`
          <section
            class="settings-general-tab"
            role="tabpanel"
            aria-label="General settings"
          >
            <article class="settings-general-tab__card">
              <h2 class="settings-general-tab__title">Preferences</h2>
              <p class="settings-general-tab__error">${message}</p>
            </article>
          </section>
        `;
      },
    });
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'app-settings-general-tab': SettingsGeneralTab;
  }
}
