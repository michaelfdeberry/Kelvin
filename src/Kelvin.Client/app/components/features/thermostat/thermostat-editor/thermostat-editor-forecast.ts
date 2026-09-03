import '../../../shared/number-input/number-input.js';

import { consume } from '@lit/context';
import { html, LitElement, TemplateResult } from 'lit';
import { customElement } from 'lit/decorators.js';
import { when } from 'lit/directives/when.js';

import thermostatEditorForecastStyles from './thermostat-editor-forecast.styles.js';
import { preferencesContext } from '../../../../contexts/preferences-context.js';
import { thermostatContext } from '../../../../contexts/thermostat-context.js';
import { Preferences } from '../../../../models/preferences.js';
import { Thermostat } from '../../../../models/thermostat.js';
import { convertToPreferredUnit, fromPreferredUnit, getPreferredUnit } from '../../../../services/utilities.js';
import sharedStyles from '../../../../shared.styles.js';

@customElement('app-thermostat-editor-forecast')
export class ThermostatEditorForecast extends LitElement {
  static override styles = [sharedStyles, thermostatEditorForecastStyles];

  @consume({ context: preferencesContext, subscribe: true })
  preferences!: Preferences;

  @consume({ context: thermostatContext, subscribe: true })
  thermostat!: Thermostat;

  private get preferredUnit(): string {
    return getPreferredUnit(this.preferences.temperatureUnit);
  }

  private get isHeatingAvailable(): boolean {
    return this.thermostat.mode === 'Heating' || this.thermostat.mode === 'Automatic';
  }

  private get isCoolingAvailable(): boolean {
    return this.thermostat.mode === 'Cooling' || this.thermostat.mode === 'Automatic';
  }

  getLockouts(): { heatingLockoutC?: number; coolingLockoutC?: number } {
    const heatingInput = this.isHeatingAvailable ? (this.shadowRoot?.getElementById('heating-lockout') as HTMLInputElement | null) : null;
    const coolingInput = this.isCoolingAvailable ? (this.shadowRoot?.getElementById('cooling-lockout') as HTMLInputElement | null) : null;

    return {
      heatingLockoutC: heatingInput?.value ? fromPreferredUnit(this.preferences.temperatureUnit, Number(heatingInput.value)) : undefined,
      coolingLockoutC: coolingInput?.value ? fromPreferredUnit(this.preferences.temperatureUnit, Number(coolingInput.value)) : undefined,
    };
  }

  override render(): TemplateResult {
    return html`
      <p class="thermostat-editor-forecast__description">
        Locks out HVAC operation based on the outdoor temperature forecast. Ensures the system does not operate when the outdoor temperature is above
        or below a certain threshold.
      </p>

      ${when(
        this.isHeatingAvailable,
        () => html`
          <div class="form-control form-control">
            <label class="form-control__label">
              Lockout Heating if outdoor temp ≥
              <app-number-input
                id="heating-lockout"
                name="heating-lockout"
                class="form-control__input"
                placeholder="${this.preferredUnit}"
                .value=${convertToPreferredUnit(this.preferences.temperatureUnit, this.thermostat.heatingLockoutC)}
              >
              </app-number-input>
            </label>
          </div>
        `,
      )}
      ${when(
        this.isCoolingAvailable,
        () => html`
          <div class="form-control form-control">
            <label class="form-control__label">
              Lockout Cooling if outdoor temp ≤
              <app-number-input
                id="cooling-lockout"
                name="cooling-lockout"
                class="form-control__input"
                placeholder="${this.preferredUnit}"
                .value=${convertToPreferredUnit(this.preferences.temperatureUnit, this.thermostat.coolingLockoutC)}
              >
              </app-number-input>
            </label>
          </div>
        `,
      )}
    `;
  }
}
