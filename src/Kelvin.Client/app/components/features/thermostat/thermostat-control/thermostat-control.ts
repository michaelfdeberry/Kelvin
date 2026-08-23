import '../thermostat-editor/thermostat-editor.js';
import '../../../shared/temperature/temperature.js';

import { consume } from '@lit/context';
import { html, LitElement, nothing, TemplateResult } from 'lit';
import { customElement, query } from 'lit/decorators.js';
import { classMap } from 'lit/directives/class-map.js';
import { unsafeSVG } from 'lit/directives/unsafe-svg.js';
import { when } from 'lit/directives/when.js';

import thermostatControlStyles from './thermostat-control.styles.js';
import editIcon from '../../../../../assets/icons/edit.svg?raw';
import { controlContext } from '../../../../contexts/control-context.js';
import { environmentReadingsContext } from '../../../../contexts/sensors-context.js';
import { schedulesContext, setPointsContext, thermostatContext } from '../../../../contexts/thermostat-context.js';
import { events } from '../../../../events.js';
import { ControlStateChange } from '../../../../models/control-state-change.js';
import { EnvironmentReading } from '../../../../models/sensors.js';
import { RunMode, Schedule, SetPoint, Thermostat } from '../../../../models/thermostat.js';
import resources from '../../../../services/api-resources.js';
import { apiPut } from '../../../../services/api.js';
import { dispatchCustomEvent, dispatchToast } from '../../../../services/utilities.js';
import sharedStyles from '../../../../shared.styles.js';
import { ThermostatEditor } from '../thermostat-editor/thermostat-editor.js';

@customElement('app-thermostat-control')
export class ThermostatControl extends LitElement {
  static override styles = [sharedStyles, thermostatControlStyles];

  @consume({ context: thermostatContext, subscribe: true })
  thermostat!: Thermostat;

  @consume({ context: setPointsContext, subscribe: true })
  setPoints!: SetPoint[];

  @consume({ context: schedulesContext, subscribe: true })
  schedules!: Schedule[];

  @consume({ context: environmentReadingsContext, subscribe: true })
  environment!: EnvironmentReading;

  @consume({ context: controlContext, subscribe: true })
  controlState!: Partial<ControlStateChange>;

  @query('app-thermostat-editor')
  private thermostatEditor!: ThermostatEditor;

  private renderSetpoint(): TemplateResult | typeof nothing {
    if (this.thermostat.mode === 'Disabled') return nothing;
    if (this.thermostat.mode === 'Off') return nothing;

    let targetTempC = this.controlState.targetTemperatureC;

    // the target temp won't be in the control state until the state changes
    // using the set points or schedules to determine the target temp for the current mode
    if (!targetTempC) {
      let setPoint: SetPoint | undefined;
      let schedule: Schedule | undefined;

      const isActive = (schedule: Schedule) => {
        const start = new Date(schedule.startTime).getTime();
        const end = new Date(schedule.endTime).getTime();
        const current = Date.now();

        if (start <= end) return current >= start && current <= end;
        return current >= start || current <= end;
      };

      if (this.controlState.state === 'Cooling') {
        setPoint = this.setPoints.find(sp => sp.type === 'Cooling');
        schedule = this.schedules.find(s => s.type === 'Cooling' && isActive(s));
      } else if (this.controlState.state === 'Heating') {
        setPoint = this.setPoints.find(sp => sp.type === 'Heating');
        schedule = this.schedules.find(s => s.type === 'Heating' && isActive(s));
      }

      targetTempC = setPoint?.targetTemperatureC ?? schedule?.targetTemperatureC;
    }

    if (!targetTempC) return nothing;

    return html`
      Set to
      <app-temperature
        .temperature=${targetTempC}
        show-unit
      ></app-temperature>
    `;
  }

  private async updateThermostat(update: Thermostat): Promise<void> {
    await apiPut<void>(resources.thermostat.updateThermostat, { body: update });
    dispatchCustomEvent(this, events.thermostatUpdated);
  }

  private async toggleFan(): Promise<void> {
    this.updateThermostat({
      ...this.thermostat,
      fanEnabled: !this.thermostat.fanEnabled,
    });
  }

  private async setMode(mode: RunMode): Promise<void> {
    this.updateThermostat({
      ...this.thermostat,
      mode: mode,
    });

    const isConfiguredForHeating = this.setPoints.some(setPoint => setPoint.type === 'Heating');
    const isConfiguredForCooling = this.setPoints.some(setPoint => setPoint.type === 'Cooling');
    const requiresHeatingConfiguration = (mode === 'Heating' || mode === 'Automatic') && !isConfiguredForHeating;
    const requiresCoolingConfiguration = (mode === 'Cooling' || mode === 'Automatic') && !isConfiguredForCooling;

    if (requiresHeatingConfiguration || requiresCoolingConfiguration) {
      this.thermostatEditor.open();
      dispatchToast(this, 'information', 'Configuration is required before this mode can be used. Please configure the thermostat settings.');
    }
  }

  override render() {
    return html`
      <div
        class="${classMap({
          thermostat: true,
          'thermostat--heating': this.controlState.state === 'Heating',
          'thermostat--cooling': this.controlState.state === 'Cooling',
        })}"
      >
        <app-thermostat-editor></app-thermostat-editor>
        <div
          class="thermostat__dial"
          aria-label="Thermostat control"
        >
          <div class="thermostat__dial-inner">
            <div class="thermostat__spacer"></div>
            <div class="thermostat__target-temp">${this.renderSetpoint()}</div>
            <div class="thermostat__current-temp">
              ${when(
                this.environment.temperatureC ?? this.controlState.environmentTemperatureC,
                () => html`
                  <app-temperature
                    temperature="${this.environment.temperatureC ?? this.controlState.environmentTemperatureC}"
                    show-unit
                  ></app-temperature>
                `,
                () => '--',
              )}
            </div>
            <div class="thermostat__status">
              ${when(this.controlState.state === 'Heating', () => html`<div class="thermostat__status-badge">🔥 HEATING</div>`)}
              ${when(this.controlState.state === 'Cooling', () => html`<div class="thermostat__status-badge">❄️ COOLING</div>`)}
            </div>
            <div class="thermostat__edit-button-container">
              ${when(
                this.thermostat.mode !== 'Disabled' && this.thermostat.mode !== 'Off',
                () => html`
                  <button
                    class="thermostat__edit-button button button--icon"
                    aria-label="Edit Settings"
                    @click=${() => this.thermostatEditor.open()}
                  >
                    ${unsafeSVG(editIcon)}
                  </button>
                `,
              )}
            </div>
          </div>
        </div>

        <div
          class="thermostat__controls"
          role="group"
          aria-label="Thermostat mode"
        >
          <button
            class=${classMap({
              button: true,
              'button--pill': true,
              thermostat__button: true,
              'thermostat__button--auto': this.thermostat.mode === 'Automatic',
            })}
            type="button"
            ?disabled=${this.thermostat.mode === 'Disabled' || this.thermostat.mode === 'Automatic'}
            @click=${() => this.setMode('Automatic')}
          >
            Auto
          </button>
          <button
            class=${classMap({
              button: true,
              'button--pill': true,
              thermostat__button: true,
              'thermostat__button--heating': this.thermostat.mode === 'Heating',
            })}
            type="button"
            ?disabled=${this.thermostat.mode === 'Disabled' || this.thermostat.mode === 'Heating'}
            @click=${() => this.setMode('Heating')}
          >
            Heat
          </button>
          <button
            class=${classMap({
              button: true,
              'button--pill': true,
              thermostat__button: true,
              'thermostat__button--cooling': this.thermostat.mode === 'Cooling',
            })}
            type="button"
            ?disabled=${this.thermostat.mode === 'Disabled' || this.thermostat.mode === 'Cooling'}
            @click=${() => this.setMode('Cooling')}
          >
            Cool
          </button>
          <button
            class=${classMap({
              button: true,
              'button--pill': true,
              thermostat__button: true,
              'thermostat__button--active': this.thermostat.mode === 'Off',
            })}
            type="button"
            ?disabled=${this.thermostat.mode === 'Disabled' || this.thermostat.mode === 'Off'}
            @click=${() => this.setMode('Off')}
          >
            Off
          </button>
        </div>
        <button
          class=${classMap({
            button: true,
            'button--pill': true,
            thermostat__button: true,
            'thermostat__button--fan': this.thermostat.fanEnabled,
          })}
          type="button"
          ?disabled=${this.thermostat.mode === 'Disabled'}
          @click=${this.toggleFan}
        >
          ${this.thermostat.fanEnabled ? 'Fan On' : 'Fan Off'}
        </button>
      </div>
    `;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-thermostat-control': ThermostatControl;
  }
}
