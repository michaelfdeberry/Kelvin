import '../thermostat-editor/thermostat-editor.js';
import '../../../shared/temperature/temperature.js';

import { consume } from '@lit/context';
import { html, LitElement, nothing, PropertyValues, TemplateResult } from 'lit';
import { customElement } from 'lit/decorators.js';
import { classMap } from 'lit/directives/class-map.js';
import { when } from 'lit/directives/when.js';

import thermostatControlStyles from './thermostat-control.styles.js';
import { controlContext } from '../../../../contexts/control-context.js';
import { environmentReadingsContext } from '../../../../contexts/sensors-context.js';
import { thermostatContext } from '../../../../contexts/thermostat-context.js';
import { events } from '../../../../events.js';
import { ControlStateChange } from '../../../../models/control-state-change.js';
import { EnvironmentReading } from '../../../../models/sensors.js';
import { RunMode, Thermostat } from '../../../../models/thermostat.js';
import { apiFetch } from '../../../../services/api.js';
import { dispatchCustomEvent } from '../../../../services/utilities.js';
import sharedStyles from '../../../../shared.styles.js';

@customElement('app-thermostat-control')
export class ThermostatControl extends LitElement {
  static override styles = [sharedStyles, thermostatControlStyles];

  private isHeating = false;
  private isCooling = false;

  @consume({ context: thermostatContext, subscribe: true })
  thermostat!: Thermostat;

  @consume({ context: environmentReadingsContext, subscribe: true })
  environment!: EnvironmentReading;

  @consume({ context: controlContext, subscribe: true })
  controlState!: Partial<ControlStateChange>;

  protected override update(changedProperties: PropertyValues): void {
    console.log('ThermostatControl update called with changedProperties:', changedProperties);
    if (changedProperties.has('controlState')) {
      console.log('Control state changed:', this.controlState);
    }

    super.update(changedProperties);
  }

  private renderSetpoint(): TemplateResult | typeof nothing {
    if (this.thermostat.mode === 'Disabled') return nothing;
    if (this.thermostat.mode === 'Off') return nothing;

    const setpointTempC: number | undefined = undefined;

    return html`
      Set to
      <app-temperature
        .temperature=${setpointTempC}
        show-unit
      ></app-temperature>
    `;
  }

  private async updateThermostat(update: Thermostat): Promise<void> {
    await apiFetch('thermostat', {
      method: 'PUT',
      body: JSON.stringify(update),
    });
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
  }

  override render() {
    return html`
      <div
        class="${classMap({
          thermostat: true,
          'thermostat--heating': this.isHeating,
          'thermostat--cooling': this.isCooling,
        })}"
      >
        <app-thermostat-editor></app-thermostat-editor>
        <div
          class="thermostat__dial"
          aria-label="Thermostat control"
        >
          <div class="thermostat__dial-inner">
            <div class="thermostat__target-temp">${this.renderSetpoint()}</div>
            <div class="thermostat__current-temp">
              ${when(
                this.environment.temperatureC,
                () => html`
                  <app-temperature
                    temperature="${this.environment.temperatureC}"
                    show-unit
                  ></app-temperature>
                `,
                () => '--',
              )}
            </div>
            <div class="thermostat__status">
              ${when(this.isHeating, () => html`<div class="thermostat__status-badge">🔥 HEATING</div>`)}
              ${when(this.isCooling, () => html`<div class="thermostat__status-badge">❄️ COOLING</div>`)}
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
  interface HTMLElementTagNameMap {
    'app-thermostat-control': ThermostatControl;
  }
}
