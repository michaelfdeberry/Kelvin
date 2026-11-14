import '../../../shared/info-bubble/info-bubble.js';
import '../../../shared/number-input/number-input.js';
import '../../../shared/toggle/toggle.js';

import { consume } from '@lit/context';
import { Task } from '@lit/task';
import { html, LitElement, TemplateResult } from 'lit';
import { customElement, query, state } from 'lit/decorators.js';

import settingsThermostatStyles from './settings-thermostat.styles.js';
import { preferencesContext } from '../../../../contexts/preferences-context.js';
import { thermostatContext } from '../../../../contexts/thermostat-context.js';
import { events } from '../../../../events.js';
import { RelayStates } from '../../../../models/gateway.js';
import { Preferences } from '../../../../models/preferences.js';
import { FanMode, Thermostat, UpdateThermostatConfigurationRequest } from '../../../../models/thermostat.js';
import apiResources from '../../../../services/api-resources.js';
import { apiGet, apiPut } from '../../../../services/api.js';
import {
  convertDeltaToPreferredUnit,
  deltaFromPreferredUnit,
  dispatchCustomEvent,
  dispatchToast,
  getPreferredUnit,
} from '../../../../services/utilities.js';
import sharedStyles from '../../../../shared.styles.js';
import { confirmModal } from '../../../shared/modal/modal-utilities.js';
import { Toggle } from '../../../shared/toggle/toggle.js';

const MS_PER_MINUTE = 60_000;
const MIN_HYSTERESIS_C = 0.3;
const MAX_HYSTERESIS_C = 2;

type ThermostatSection = 'hysteresis' | 'co2' | 'balance';

@customElement('app-settings-thermostat')
export class SettingsThermostat extends LitElement {
  static override styles = [sharedStyles, settingsThermostatStyles];

  @consume({ context: thermostatContext, subscribe: true })
  private thermostat!: Thermostat;

  @consume({ context: preferencesContext, subscribe: true })
  private preferences!: Preferences;

  @query('.settings-thermostat__control')
  private controlToggle!: Toggle;

  // Unsaved toggle edits; undefined means "follow the saved thermostat".
  @state()
  private co2EnabledDraft?: boolean;

  @state()
  private balanceEnabledDraft?: boolean;

  private get co2Enabled(): boolean {
    return this.co2EnabledDraft ?? this.thermostat.cO2RecirculationEnabled;
  }

  private get balanceEnabled(): boolean {
    return this.balanceEnabledDraft ?? this.thermostat.heatingAutoBalanceEnabled;
  }

  private clearDraft(section: ThermostatSection): void {
    if (section === 'co2') this.co2EnabledDraft = undefined;
    if (section === 'balance') this.balanceEnabledDraft = undefined;
  }

  private relayStatesTask = new Task(this, {
    task: async ([,], { signal }) => apiGet<RelayStates>(apiResources.gateways.getRelayStates, { signal }),
    args: () => [this.thermostat?.mode],
  });

  private async handleSave(section: ThermostatSection, event: Event): Promise<void> {
    event.preventDefault();

    const data = new FormData(event.target as HTMLFormElement);
    const unit = this.preferences.temperatureUnit;
    const value = (name: string): number => Number(data.get(name));
    const { thermostat } = this;

    // Each card saves on its own; everything outside the card keeps its saved value.
    const request: UpdateThermostatConfigurationRequest = {
      fanMode: thermostat.fanMode,
      hysteresisC: thermostat.hysteresisC,
      cO2RecirculationEnabled: thermostat.cO2RecirculationEnabled,
      cO2RecirculationTriggerPpm: thermostat.cO2RecirculationTriggerPpm,
      cO2RecirculationSatisfiedPpm: thermostat.cO2RecirculationSatisfiedPpm,
      cO2RecirculationDurationMs: thermostat.cO2RecirculationDurationMs,
      heatingAutoBalanceEnabled: thermostat.heatingAutoBalanceEnabled,
      heatingBalanceTriggerC: thermostat.heatingBalanceTriggerC,
      heatingBalanceSatisfiedC: thermostat.heatingBalanceSatisfiedC,
      heatingAutoBalanceDurationMs: thermostat.heatingAutoBalanceDurationMs,
    };

    if (section === 'hysteresis') {
      request.fanMode = data.get('fanMode') as FanMode;
      request.hysteresisC = deltaFromPreferredUnit(unit, value('hysteresis'));
    } else if (section === 'co2') {
      request.cO2RecirculationEnabled = this.co2Enabled;

      // Disabled controls are left out of FormData, so a switched-off card keeps its saved values.
      if (this.co2Enabled) {
        request.cO2RecirculationTriggerPpm = Math.round(value('co2RecirculationTrigger'));
        request.cO2RecirculationSatisfiedPpm = Math.round(value('co2RecirculationSatisfied'));
        request.cO2RecirculationDurationMs = Math.round(value('co2RecirculationDuration') * MS_PER_MINUTE);
      }
    } else {
      request.heatingAutoBalanceEnabled = this.balanceEnabled;

      if (this.balanceEnabled) {
        request.heatingBalanceTriggerC = deltaFromPreferredUnit(unit, value('heatingBalanceTrigger'));
        request.heatingBalanceSatisfiedC = deltaFromPreferredUnit(unit, value('heatingBalanceSatisfied'));
        request.heatingAutoBalanceDurationMs = Math.round(value('heatingAutoBalanceDuration') * MS_PER_MINUTE);
      }
    }

    try {
      await apiPut<void>(apiResources.thermostat.updateThermostatConfiguration, { body: request });
      this.clearDraft(section);
      dispatchCustomEvent(this, events.thermostatUpdated);
      dispatchToast(this, 'Success', 'Thermostat settings saved successfully.');
    } catch {
      // apiPut already surfaces an error toast.
    }
  }

  private async handleControlChange(): Promise<void> {
    const updatedThermostat: Thermostat = { ...this.thermostat };

    if (this.controlToggle.checked) {
      const result = await confirmModal('Enabling HVAC control will allow the system to control your HVAC. Are you sure you want to enable it?');
      if (!result) {
        this.controlToggle.checked = false;
        return;
      }
      updatedThermostat.mode = 'Off';
    } else {
      const result = await confirmModal(
        `
          Disabling HVAC control will prevent the system from controlling your HVAC and revert control to the failsafe thermostat.
          Are you sure you want to disable it?
        `,
      );
      if (!result) {
        this.controlToggle.checked = true;
        return;
      }
      updatedThermostat.mode = 'Disabled';
    }

    await apiPut(apiResources.thermostat.updateThermostat, { body: updatedThermostat });
    dispatchCustomEvent(this, events.thermostatUpdated);
  }

  private renderDelta(id: string, label: string, deltaC: number, disabled: boolean): TemplateResult {
    const unit = this.preferences.temperatureUnit;

    return html`
      <div class="form-control">
        <label class="form-control__label">
          ${label} (${getPreferredUnit(unit)})
          <app-number-input
            id=${id}
            name=${id}
            step="0.1"
            class="form-control__input"
            ?disabled=${disabled}
            .value=${convertDeltaToPreferredUnit(unit, deltaC)}
          ></app-number-input>
        </label>
      </div>
    `;
  }

  private renderActions(): TemplateResult {
    return html`
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
        >
          Save
        </button>
      </div>
    `;
  }

  private renderHysteresisForm(): TemplateResult {
    const unit = this.preferences.temperatureUnit;

    return html`
      <form
        @submit=${(event: Event) => this.handleSave('hysteresis', event)}
        @reset=${() => this.clearDraft('hysteresis')}
      >
        <div class="form-group">
          <div class="form-control settings-thermostat__help-field">
            <label class="form-control__label">
              <span id="hysteresisLabel">Hysteresis (${getPreferredUnit(unit)})</span>
              <app-number-input
                id="hysteresis"
                name="hysteresis"
                aria-labelledby="hysteresisLabel"
                step="0.1"
                class="form-control__input"
                .min=${convertDeltaToPreferredUnit(unit, MIN_HYSTERESIS_C)}
                .max=${convertDeltaToPreferredUnit(unit, MAX_HYSTERESIS_C)}
                .value=${convertDeltaToPreferredUnit(unit, this.thermostat.hysteresisC)}
              ></app-number-input>
            </label>
            <app-info-bubble label="About hysteresis">
              How far the temperature may drift from the target before heating or cooling starts and stops.
            </app-info-bubble>
          </div>
          <div class="form-control">
            <div class="settings-thermostat__field-label">
              <label
                class="form-control__label"
                for="fanMode"
                >System Type</label
              >
              <app-info-bubble label="About system type">
                <p>The system type determines how the fan operates in different modes.</p>
                <b>Equipment Controlled</b>
                <div>
                  <p>
                    Choose this option if your system automatically controls the fan based on the current mode and equipment settings. In this mode,
                    Kelvin will not turn on the fan when there is a call for heating or cooling. This is an atypical setup.
                  </p>
                </div>
                <b>Fossil Fuel System</b>
                <div>
                  <p>
                    This is typical for gas or oil fired systems. In this mode, the control board manages the fan during heating, and Kelvin turns on
                    the fan during cooling.
                  </p>
                </div>
                <b>Electric System</b>
                <div>
                  <p>This is typical for electric or heat pump systems. In this mode, Kelvin turns on the fan during both heating and cooling.</p>
                </div>
                <p>Manual fan, Recirculation, and Balancing features can run the fan in any mode.</p>
              </app-info-bubble>
            </div>
            <select
              id="fanMode"
              name="fanMode"
              class="form-control__input select"
            >
              <option
                value="Manual"
                ?selected=${this.thermostat.fanMode === 'Manual'}
              >
                Equipment Controlled
              </option>
              <option
                value="CoolingOnly"
                ?selected=${this.thermostat.fanMode === 'CoolingOnly'}
              >
                Fossil Fuel System
              </option>
              <option
                value="HeatingAndCooling"
                ?selected=${this.thermostat.fanMode === 'HeatingAndCooling'}
              >
                Electric System
              </option>
            </select>
          </div>
        </div>
        ${this.renderActions()}
      </form>
    `;
  }

  private renderRecirculationCard(): TemplateResult {
    const thermostat = this.thermostat;
    const disabled = !this.co2Enabled;

    return html`
      <article class="card">
        <div class="card__header">
          <h2 class="card__title">CO<sub>2</sub> Recirculation</h2>
          <label class="settings-thermostat__toggle-label">
            Enabled
            <app-toggle
              ?checked=${this.co2Enabled}
              @change=${(event: Event) => (this.co2EnabledDraft = (event.target as Toggle).checked)}
            ></app-toggle>
          </label>
        </div>
        <form
          @submit=${(event: Event) => this.handleSave('co2', event)}
          @reset=${() => this.clearDraft('co2')}
        >
          <div class="form-group">
            <p class="settings-thermostat__description">
              Cycles the fan to circulate air when a high CO<sub>2</sub> level is detected by any sensor.
            </p>
            <div class="settings-thermostat__grid">
              <div class="form-control">
                <label class="form-control__label">
                  Start above (ppm)
                  <app-number-input
                    id="co2RecirculationTrigger"
                    name="co2RecirculationTrigger"
                    step="50"
                    class="form-control__input"
                    ?disabled=${disabled}
                    .value=${thermostat.cO2RecirculationTriggerPpm}
                  ></app-number-input>
                </label>
              </div>
              <div class="form-control">
                <label class="form-control__label">
                  Stop at or below (ppm)
                  <app-number-input
                    id="co2RecirculationSatisfied"
                    name="co2RecirculationSatisfied"
                    step="50"
                    class="form-control__input"
                    ?disabled=${disabled}
                    .value=${thermostat.cO2RecirculationSatisfiedPpm}
                  ></app-number-input>
                </label>
              </div>
              <div class="form-control">
                <label class="form-control__label">
                  Cycle Duration (minutes)
                  <app-number-input
                    id="co2RecirculationDuration"
                    name="co2RecirculationDuration"
                    step="0.5"
                    class="form-control__input"
                    ?disabled=${disabled}
                    .value=${thermostat.cO2RecirculationDurationMs / MS_PER_MINUTE}
                  ></app-number-input>
                </label>
              </div>
            </div>
          </div>
          ${this.renderActions()}
        </form>
      </article>
    `;
  }

  private renderBalanceCard(): TemplateResult {
    const thermostat = this.thermostat;
    const disabled = !this.balanceEnabled;

    return html`
      <article class="card">
        <div class="card__header">
          <h2 class="card__title">Heating Balance</h2>
          <label class="settings-thermostat__toggle-label">
            Enabled
            <app-toggle
              ?checked=${this.balanceEnabled}
              @change=${(event: Event) => (this.balanceEnabledDraft = (event.target as Toggle).checked)}
            ></app-toggle>
          </label>
        </div>
        <form
          @submit=${(event: Event) => this.handleSave('balance', event)}
          @reset=${() => this.clearDraft('balance')}
        >
          <div class="form-group">
            <p class="settings-thermostat__description">Runs the fan after a heating cycle to even out temperatures when areas differ by too much.</p>
            <div class="settings-thermostat__grid">
              ${this.renderDelta('heatingBalanceTrigger', 'Start when areas differ by more than', thermostat.heatingBalanceTriggerC, disabled)}
              ${this.renderDelta('heatingBalanceSatisfied', 'Stop when areas differ by', thermostat.heatingBalanceSatisfiedC, disabled)}
              <div class="form-control">
                <label class="form-control__label">
                  Maximum run time (minutes)
                  <app-number-input
                    id="heatingAutoBalanceDuration"
                    name="heatingAutoBalanceDuration"
                    step="0.5"
                    class="form-control__input"
                    ?disabled=${disabled}
                    .value=${thermostat.heatingAutoBalanceDurationMs / MS_PER_MINUTE}
                  ></app-number-input>
                </label>
              </div>
            </div>
          </div>
          ${this.renderActions()}
        </form>
      </article>
    `;
  }

  override render() {
    if (!this.thermostat || !this.preferences) {
      return html`
        <section class="settings-thermostat">
          <article class="card"><p>Loading thermostat settings...</p></article>
        </section>
      `;
    }

    return html`
      <section
        class="settings-thermostat"
        aria-label="Thermostat settings"
      >
        <article class="card">
          <div class="card__header">
            <h2 class="card__title">Thermostat Settings</h2>
            <label class="settings-thermostat__toggle-label">
              HVAC Control
              ${this.relayStatesTask.render({
                complete: relayStates => html`
                  <app-toggle
                    class="settings-thermostat__control"
                    @change=${this.handleControlChange}
                    ?checked=${relayStates?.control ?? false}
                  ></app-toggle>
                `,
                pending: () => html`<app-toggle disabled></app-toggle>`,
                error: () => html`<span class="error">Unavailable</span>`,
              })}
            </label>
          </div>
          ${this.renderHysteresisForm()}
        </article>
        ${this.renderRecirculationCard()} ${this.renderBalanceCard()}
      </section>
    `;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions
  interface HTMLElementTagNameMap {
    'app-settings-thermostat': SettingsThermostat;
  }
}
