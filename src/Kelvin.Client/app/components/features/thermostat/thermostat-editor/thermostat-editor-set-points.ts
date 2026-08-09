import { consume } from '@lit/context';
import { html, LitElement, TemplateResult } from 'lit';
import { customElement } from 'lit/decorators.js';
import { when } from 'lit/directives/when.js';

import thermostatEditorSetPointsStyles from './thermostat-editor-set-points.styles.js';
import { preferencesContext } from '../../../../contexts/preferences-context.js';
import { setPointsContext, thermostatContext } from '../../../../contexts/thermostat-context.js';
import { Preferences } from '../../../../models/preferences.js';
import { SetPoint, SetPointInput, Thermostat } from '../../../../models/thermostat.js';
import { fromPreferredUnit, getPreferredUnit, toPreferredUnit } from '../../../../services/utilities.js';
import sharedStyles from '../../../../shared.styles.js';

@customElement('app-thermostat-editor-set-points')
export class ThermostatEditorSetPoints extends LitElement {
  static override styles = [sharedStyles, thermostatEditorSetPointsStyles];

  @consume({ context: preferencesContext, subscribe: true })
  preferences!: Preferences;

  @consume({ context: thermostatContext, subscribe: true })
  thermostat!: Thermostat;

  @consume({ context: setPointsContext, subscribe: true })
  setPoints!: SetPoint[];

  private get heatingSetPoint(): SetPoint | undefined {
    return this.setPoints.find(sp => sp.type === 'Heating');
  }

  private get coolingSetPoint(): SetPoint | undefined {
    return this.setPoints.find(sp => sp.type === 'Cooling');
  }

  private get preferredUnit(): string {
    return getPreferredUnit(this.preferences.temperatureUnit);
  }

  private get isHeatingAvailable(): boolean {
    return this.thermostat.mode === 'Heating' || this.thermostat.mode === 'Automatic';
  }

  private get isCoolingAvailable(): boolean {
    return this.thermostat.mode === 'Cooling' || this.thermostat.mode === 'Automatic';
  }

  getSetPoints(): SetPointInput[] {
    const setPoints: SetPointInput[] = [];

    if (this.isHeatingAvailable) {
      const input = this.shadowRoot?.getElementById('heating-setpoint') as HTMLInputElement | null;
      if (input?.value) {
        setPoints.push({
          id: this.heatingSetPoint?.id,
          type: 'Heating',
          targetTemperatureC: fromPreferredUnit(this.preferences.temperatureUnit, Number(input.value)),
        });
      }
    }

    if (this.isCoolingAvailable) {
      const input = this.shadowRoot?.getElementById('cooling-setpoint') as HTMLInputElement | null;
      if (input?.value) {
        setPoints.push({
          id: this.coolingSetPoint?.id,
          type: 'Cooling',
          targetTemperatureC: fromPreferredUnit(this.preferences.temperatureUnit, Number(input.value)),
        });
      }
    }

    return setPoints;
  }

  override render(): TemplateResult {
    return html`
      <p class="thermostat-editor-set-points__description">Set Points are used when no schedule is active.</p>
      ${when(
        this.isHeatingAvailable,
        () => html`
          <div class="form-control form-control">
            <label class="form-control__label">
              Heating Set Point
              <input
                type="number"
                id="heating-setpoint"
                name="heating-setpoint"
                class="form-control__input input"
                placeholder="${this.preferredUnit}"
                .value=${toPreferredUnit(this.preferences.temperatureUnit, this.heatingSetPoint?.targetTemperatureC)}
              />
            </label>
          </div>
        `,
      )}
      ${when(
        this.isCoolingAvailable,
        () => html`
          <div class="form-control form-control">
            <label class="form-control__label">
              Cooling Set Point
              <input
                type="number"
                id="cooling-setpoint"
                name="cooling-setpoint"
                class="form-control__input input"
                placeholder="${this.preferredUnit}"
                .value=${toPreferredUnit(this.preferences.temperatureUnit, this.coolingSetPoint?.targetTemperatureC)}
              />
            </label>
          </div>
        `,
      )}
    `;
  }
}
