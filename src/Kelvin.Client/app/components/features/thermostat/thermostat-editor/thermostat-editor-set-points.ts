import '../../../shared/temperature-slider/temperature-slider.js';

import { consume } from '@lit/context';
import { html, LitElement, TemplateResult } from 'lit';
import { customElement, query } from 'lit/decorators.js';
import { classMap } from 'lit/directives/class-map.js';
import { when } from 'lit/directives/when.js';

import thermostatEditorSetPointsStyles from './thermostat-editor-set-points.styles.js';
import { preferencesContext } from '../../../../contexts/preferences-context.js';
import { setPointsContext, thermostatContext } from '../../../../contexts/thermostat-context.js';
import { Preferences } from '../../../../models/preferences.js';
import { SetPoint, SetPointInput, Thermostat } from '../../../../models/thermostat.js';
import { convertToPreferredUnit, fromPreferredUnit, getPreferredUnit, toPreferredUnit } from '../../../../services/utilities.js';
import sharedStyles from '../../../../shared.styles.js';
import { TemperatureSlider } from '../../../shared/temperature-slider/temperature-slider.js';

@customElement('app-thermostat-editor-set-points')
export class ThermostatEditorSetPoints extends LitElement {
  static override styles = [sharedStyles, thermostatEditorSetPointsStyles];

  @consume({ context: preferencesContext, subscribe: true })
  preferences!: Preferences;

  @consume({ context: thermostatContext, subscribe: true })
  thermostat!: Thermostat;

  @consume({ context: setPointsContext, subscribe: true })
  setPoints!: SetPoint[];

  @query('app-temperature-slider')
  private slider?: TemperatureSlider;

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

  private get isRangeMode(): boolean {
    return this.isHeatingAvailable && this.isCoolingAvailable;
  }

  private get heatingSliderValue(): number {
    return convertToPreferredUnit(this.preferences.temperatureUnit, this.heatingSetPoint?.targetTemperatureC) ?? 0;
  }

  private get coolingSliderValue(): number {
    return convertToPreferredUnit(this.preferences.temperatureUnit, this.coolingSetPoint?.targetTemperatureC) ?? 0;
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

  // The slider is a visual aid only - it writes into the number inputs rather than submitting its own form value.
  private handleSliderInput(): void {
    const slider = this.slider;
    if (!slider) return;

    if (this.isRangeMode) {
      this.setInputValue('heating-setpoint', slider.valueLow);
      this.setInputValue('cooling-setpoint', slider.valueHigh);
    } else if (this.isHeatingAvailable) {
      this.setInputValue('heating-setpoint', slider.value);
    } else if (this.isCoolingAvailable) {
      this.setInputValue('cooling-setpoint', slider.value);
    }
  }

  private setInputValue(id: string, value: number): void {
    const input = this.shadowRoot?.getElementById(id) as HTMLInputElement | null;
    if (input) input.value = value.toFixed(1);
  }

  override render(): TemplateResult {
    return html`
      <div
        class="${classMap({
          'thermostat-editor-set-points': true,
          'thermostat-editor-set-points--range': this.isRangeMode,
          'thermostat-editor-set-points--heating-only': this.isHeatingAvailable && !this.isCoolingAvailable,
          'thermostat-editor-set-points--cooling-only': this.isCoolingAvailable && !this.isHeatingAvailable,
        })}"
      >
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
      </div>
    `;
  }
}

//  ${when(
//           this.isHeatingAvailable || this.isCoolingAvailable,
//           () => html`
//             <app-temperature-slider
//               class="thermostat-editor-set-points__slider"
//               ?heating=${this.isHeatingAvailable}
//               ?cooling=${this.isCoolingAvailable}
//               ?range=${this.isRangeMode}
//               .value=${this.isHeatingAvailable ? this.heatingSliderValue : this.coolingSliderValue}
//               .valueLow=${this.heatingSliderValue}
//               .valueHigh=${this.coolingSliderValue}
//               low-label="Heating set point"
//               high-label="Cooling set point"
//               @input=${this.handleSliderInput}
//             ></app-temperature-slider>
//           `,
//         )}
