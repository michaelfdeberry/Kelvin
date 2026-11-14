import './humidifier-editor.js';

import { consume } from '@lit/context';
import { html, LitElement, nothing, PropertyValues, TemplateResult } from 'lit';
import { customElement, property, query, state } from 'lit/decorators.js';

import { HumidifierEditor, MONTHS } from './humidifier-editor.js';
import settingsHumidifierStyles from './settings-humidifier.styles.js';
import { devicesContext } from '../../../../contexts/devices-context.js';
import { preferencesContext } from '../../../../contexts/preferences-context.js';
import { Device, Humidifier, Humidifiers } from '../../../../models/devices.js';
import { Preferences } from '../../../../models/preferences.js';
import apiResources from '../../../../services/api-resources.js';
import { apiGet } from '../../../../services/api.js';
import { convertToPreferredUnit, getPreferredUnit } from '../../../../services/utilities.js';
import sharedStyles from '../../../../shared.styles.js';

@customElement('app-settings-humidifier')
export class SettingsHumidifier extends LitElement {
  static override styles = [sharedStyles, settingsHumidifierStyles];

  // The device list changing is the cue to reload the humidifiers' configuration.
  @consume({ context: devicesContext, subscribe: true })
  @property({ attribute: false })
  private devices?: Device[];

  @consume({ context: preferencesContext, subscribe: true })
  @property({ attribute: false })
  private preferences?: Preferences;

  @query('app-humidifier-editor')
  private editor!: HumidifierEditor;

  @state()
  private humidifiers: Humidifier[] = [];

  private loadRequest = 0;

  override willUpdate(changed: PropertyValues): void {
    if (changed.has('devices')) {
      void this.load();
    }
  }

  private async load(): Promise<void> {
    const request = ++this.loadRequest;
    if (!this.devices?.some(device => device.type === 'Humidifier')) {
      this.humidifiers = [];
      return;
    }

    try {
      const response = await apiGet<Humidifiers>(apiResources.humidifiers.getHumidifiers);
      if (request === this.loadRequest) this.humidifiers = response.humidifiers;
    } catch {
      // apiGet already surfaces an error toast.
    }
  }

  private renderHumidifier(humidifier: Humidifier): TemplateResult | typeof nothing {
    if (!humidifier.enabled) return nothing;

    const unit = this.preferences!.temperatureUnit;
    return html`
      <article class="card">
        <div class="card__header">
          <h2 class="card__title">${humidifier.name}</h2>
          <button
            type="button"
            class="button button--secondary"
            @click=${() => this.editor.open(humidifier)}
          >
            Edit
          </button>
        </div>
        <p class="settings-humidifier__description">
          Climate configuration for the Humidifier device. Additional settings are available on the
          <a href="/settings/devices">Devices page</a>.
        </p>
        <dl class="settings-humidifier__summary">
          <div>
            <dt>Season</dt>
            <dd>${MONTHS[humidifier.startMonth - 1]} to ${MONTHS[humidifier.endMonth - 1]}</dd>
          </div>
        </dl>
        <div class="table-container">
          <table class="table">
            <thead>
              <tr>
                <th>Outdoor temperature</th>
                <th>Target humidity</th>
              </tr>
            </thead>
            <tbody>
              ${humidifier.targets.map(
                target => html`
                  <tr>
                    <td data-label="Outdoor temperature">
                      At or below ${Math.round(convertToPreferredUnit(unit, target.outdoorTemperatureC)!)}${getPreferredUnit(unit)}
                    </td>
                    <td data-label="Target humidity">${target.humidityPercentage}%</td>
                  </tr>
                `,
              )}
            </tbody>
          </table>
        </div>
      </article>
    `;
  }

  override render(): TemplateResult | typeof nothing {
    if (!this.preferences || !this.humidifiers.length) return nothing;

    return html`
      <section
        class="settings-humidifier"
        aria-label="Humidifier settings"
      >
        ${this.humidifiers.map(humidifier => this.renderHumidifier(humidifier))}
        <app-humidifier-editor></app-humidifier-editor>
      </section>
    `;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-settings-humidifier': SettingsHumidifier;
  }
}
