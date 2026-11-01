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
import { callContext } from '../../../../contexts/control-context.js';
import { environmentReadingsContext } from '../../../../contexts/sensors-context.js';
import { schedulesContext, setPointsContext, thermostatContext } from '../../../../contexts/thermostat-context.js';
import { events } from '../../../../events.js';
import { ControlStateChange } from '../../../../models/control-state-change.js';
import { EnvironmentReading } from '../../../../models/sensors.js';
import { RunMode, Schedule, SetPoint, Thermostat } from '../../../../models/thermostat.js';
import { ToastDetail } from '../../../../models/toast-detail.js';
import resources from '../../../../services/api-resources.js';
import { apiPut } from '../../../../services/api.js';
import { dispatchCustomEvent, dispatchToast, timestampToDate } from '../../../../services/utilities.js';
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

  @consume({ context: callContext, subscribe: true })
  callState!: Partial<ControlStateChange>;

  @query('app-thermostat-editor')
  private thermostatEditor!: ThermostatEditor;

  private controlTargetTempC?: number;

  private currentControlState?: 'Heating' | 'Cooling';

  protected override updated(): void {
    // this component really only cares about the call changes
    // so track the call state and temp independently
    if (this.callState.targetTemperatureC !== undefined) {
      this.controlTargetTempC = this.callState.targetTemperatureC;
    }

    if (this.callState.state !== undefined && ['Heating', 'Cooling', 'Dwell'].includes(this.callState.state)) {
      if (this.callState.state === 'Dwell') {
        this.currentControlState = undefined;
      } else {
        this.currentControlState = this.callState.state === 'Heating' ? 'Heating' : 'Cooling';
      }
    }
  }

  private getTargetTemp() {
    /**
     * This kind of replicates the backend logic for finding the target setpoint.
     * I don't really like it being client side, but the set point is figured out in real time on the backend
     * when processing the temperature.
     */
    let setPoint = this.controlTargetTempC;
    let mode = this.thermostat.mode;

    if (!setPoint) {
      if (mode === 'Automatic') {
        // currentControlState can be dwell, or off, so check previousState too
        if (this.currentControlState === 'Heating' || this.callState.previousState === 'Heating') {
          mode = 'Heating';
        } else if (this.currentControlState === 'Cooling' || this.callState.previousState === 'Cooling') {
          mode = 'Cooling';
        }
      }

      const now = new Date();
      const isActive = (start: string, end: string) => {
        const startDate = timestampToDate(start);
        const endDate = timestampToDate(end);

        if (startDate <= endDate) {
          return now >= startDate && now <= endDate;
        }
        return now >= startDate || now <= endDate;
      };

      const findTarget = (mode: RunMode) => {
        const schedule = this.schedules.find(s => s.type === mode && isActive(s.startTime, s.endTime));
        const setPoint = this.setPoints.find(s => s.type === mode);
        return schedule?.targetTemperatureC ?? setPoint?.targetTemperatureC;
      };

      if (mode === 'Heating') {
        setPoint = findTarget('Heating');
      } else if (mode === 'Cooling') {
        setPoint = findTarget('Cooling');
      }
    }

    return setPoint;
  }

  private renderSetpoint(): TemplateResult | typeof nothing {
    const targetTemp = this.getTargetTemp();
    if (!targetTemp) return nothing;

    return html`
      Target
      <app-temperature
        .temperature=${targetTemp}
        show-unit
      ></app-temperature>
    `;
  }

  private async updateThermostat(update: Thermostat): Promise<void> {
    await apiPut<void>(resources.thermostat.updateThermostat, { body: update });
    dispatchCustomEvent(this, events.thermostatUpdated);
  }

  private async toggleFan(): Promise<void> {
    await this.updateThermostat({
      ...this.thermostat,
      fanEnabled: !this.thermostat.fanEnabled,
    });
  }

  private async setMode(mode: RunMode): Promise<void> {
    const isConfiguredForHeating = this.setPoints.some(setPoint => setPoint.type === 'Heating');
    const isConfiguredForCooling = this.setPoints.some(setPoint => setPoint.type === 'Cooling');
    const requiresHeatingConfiguration = (mode === 'Heating' || mode === 'Automatic') && !isConfiguredForHeating;
    const requiresCoolingConfiguration = (mode === 'Cooling' || mode === 'Automatic') && !isConfiguredForCooling;

    const requiresConfigurationToast: ToastDetail = {
      type: 'Information',
      message: 'Configuration is required before this mode can be used. Please configure the thermostat settings.',
      duration: 5000,
      dismissible: true,
    };

    if (requiresHeatingConfiguration || requiresCoolingConfiguration) {
      dispatchToast(this, requiresConfigurationToast);

      const result = await this.thermostatEditor.open(mode);
      if (!result) return;

      requestAnimationFrame(() => this.setMode(mode));
      return;
    }

    try {
      await this.updateThermostat({ ...this.thermostat, mode: mode });
    } catch (error) {
      const validationErrorCodes = [
        'ValidateThermostatSafety.DuplicateSetPointType',
        'ValidateThermostatSafety.OverlappingSchedulesSameType',
        'ValidateThermostatSafety.UnsafeTargetOverlap',
        'ValidateThermostatSafety.UnsafeActivationOverlap',
      ];
      const errorCode = error instanceof Error ? (error as Error).message : '';
      if (validationErrorCodes.includes(errorCode)) {
        dispatchToast(this, requiresConfigurationToast);

        const result = await this.thermostatEditor.open(mode);
        if (!result) return;

        requestAnimationFrame(() => this.setMode(mode));
      }
    }
  }

  override render() {
    return html`
      <app-thermostat-editor></app-thermostat-editor>
      <div
        class="${classMap({
          thermostat: true,
          'thermostat--heating': this.currentControlState === 'Heating',
          'thermostat--cooling': this.currentControlState === 'Cooling',
        })}"
      >
        <div class="thermostat__header">
          <div class="thermostat__target-temp">${this.renderSetpoint()}</div>
          <div class="thermostat__status">
            ${when(this.currentControlState === 'Heating', () => html`<div class="thermostat__status-badge">🔥 HEATING</div>`)}
            ${when(this.currentControlState === 'Cooling', () => html`<div class="thermostat__status-badge">❄️ COOLING</div>`)}
          </div>
        </div>
        <div
          class="thermostat__state number"
          aria-label="Thermostat control"
        >
          <div class="thermostat__temps">
            <div class="thermostat__current-temp">
              ${when(
                this.environment.temperatureC ?? this.callState.environmentTemperatureC,
                () => html`
                  <app-temperature
                    temperature="${this.environment.temperatureC ?? this.callState.environmentTemperatureC}"
                    show-unit
                  ></app-temperature>
                `,
                () => '--',
              )}
            </div>
          </div>
          <div class="thermostat__air-quality ">
            <div class="thermostat__air-quality-item">
              <div class="text-muted">Humidity</div>
              <div class="thermostat__air-quality-value">${this.environment.humidityPercentage?.toFixed(1)}<span class="unit">%</span></div>
            </div>
            <div class="thermostat__air-quality-item">
              <div class="text-muted">CO<sub>2</sub></div>
              <div class="thermostat__air-quality-value">${this.environment.cO2LevelPpm?.toFixed(1)}<span class="unit">ppm</span></div>
            </div>
          </div>
        </div>

        <div
          class="thermostat__controls"
          role="group"
          aria-label="Thermostat mode"
        >
          <div class="thermostat__button-group">
            <button
              class=${classMap({
                button: true,
                thermostat__button: true,
                'thermostat__button--auto': this.thermostat.mode === 'Automatic',
                'thermostat__button--active': this.thermostat.mode === 'Automatic',
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
                thermostat__button: true,
                'thermostat__button--heating': this.thermostat.mode === 'Heating',
                'thermostat__button--active': this.thermostat.mode === 'Heating',
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
                thermostat__button: true,
                'thermostat__button--cooling': this.thermostat.mode === 'Cooling',
                'thermostat__button--active': this.thermostat.mode === 'Cooling',
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
              thermostat__button: true,
              'thermostat__button--fan': true,
              'thermostat__button--active': this.thermostat.fanEnabled,
            })}
            type="button"
            ?disabled=${this.thermostat.mode === 'Disabled'}
            @click=${this.toggleFan}
          >
            ${this.thermostat.fanEnabled ? 'Fan: On' : 'Fan: Auto'}
          </button>
          <button
            class="button thermostat__button thermostat__button--edit"
            aria-label="Edit Settings"
            .disabled=${this.thermostat.mode === 'Disabled' || this.thermostat.mode === 'Off'}
            @click=${() => this.thermostatEditor.open()}
          >
            ${unsafeSVG(editIcon)}
          </button>
        </div>
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
