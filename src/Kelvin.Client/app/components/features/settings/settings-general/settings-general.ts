import { consume } from '@lit/context';
import { html, LitElement } from 'lit';
import { customElement, query } from 'lit/decorators.js';

import settingsGeneralTabStyles from './settings-general.styles.js';
import { preferencesContext } from '../../../../contexts/preferences-context.js';
import { events } from '../../../../events.js';
import { Preferences } from '../../../../models/preferences.js';
import resources from '../../../../services/api-resources.js';
import { apiPut } from '../../../../services/api.js';
import { dispatchCustomEvent, dispatchToast } from '../../../../services/utilities.js';
import sharedStyles from '../../../../shared.styles.js';

@customElement('app-settings-general')
export class SettingsGeneral extends LitElement {
  static override styles = [sharedStyles, settingsGeneralTabStyles];

  @consume({ context: preferencesContext, subscribe: true })
  preferences!: Preferences;

  @query('#temperatureUnit') temperatureUnitSelect!: HTMLSelectElement;
  @query('#timeFormat') timeFormatSelect!: HTMLSelectElement;

  private async savePreferences(event: Event) {
    event.preventDefault();

    const temperatureUnit = this.temperatureUnitSelect.value as 'Celsius' | 'Fahrenheit';
    const timeFormat = this.timeFormatSelect.value as 'Hour24' | 'Hour12';
    if (this.preferences.temperatureUnit === temperatureUnit && this.preferences.timeFormat === timeFormat) {
      return;
    }

    const preferences = { ...this.preferences, temperatureUnit, timeFormat };
    await apiPut<void>(resources.preferences.updatePreferences, {
      body: preferences,
    });

    dispatchCustomEvent(this, events.preferencesSaved, preferences);
    dispatchToast(this, 'success', 'Preferences saved successfully.', { dismissible: true, duration: 3000 });
  }

  override render() {
    if (!this.preferences) {
      return html`<p>Loading preferences...</p>`;
    }

    return html`
      <section
        class="settings-general"
        role="tabpanel"
        aria-label="General settings"
      >
        <article class="card">
          <h2 class="card__title">Preferences</h2>
          <form class="form-group">
            <div class="form-control">
              <label class="form-control__label">
                Temperature unit
                <select
                  id="temperatureUnit"
                  name="temperatureUnit"
                  class="form-control__input select"
                  aria-label="Temperature unit"
                >
                  <option
                    ?selected=${this.preferences.temperatureUnit === 'Celsius'}
                    value="Celsius"
                  >
                    Celsius
                  </option>
                  <option
                    ?selected=${this.preferences.temperatureUnit === 'Fahrenheit'}
                    value="Fahrenheit"
                  >
                    Fahrenheit
                  </option>
                </select>
              </label>
            </div>
            <div class="form-control">
              <label class="form-control__label">
                Time format
                <select
                  id="timeFormat"
                  name="timeFormat"
                  class="form-control__input select"
                  aria-label="Time format"
                >
                  <option
                    ?selected=${this.preferences.timeFormat === 'Hour24'}
                    value="Hour24"
                  >
                    24-hour
                  </option>
                  <option
                    ?selected=${this.preferences.timeFormat === 'Hour12'}
                    value="Hour12"
                  >
                    12-hour
                  </option>
                </select>
              </label>
            </div>
            <div class="form-group__actions">
              <button
                type="reset"
                class="button button--secondary"
              >
                Reset
              </button>
              <button
                type="submit"
                class="button button--primary"
                @click=${this.savePreferences}
              >
                Save
              </button>
            </div>
          </form>
        </article>
      </section>
    `;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-settings-general': SettingsGeneral;
  }
}
