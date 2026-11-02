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
import { CO2RecirculationMode, Thermostat, UpdateThermostatConfigurationRequest } from '../../../../models/thermostat.js';
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
    return this.co2EnabledDraft ?? this.thermostat.co2RecirculationEnabled;
  }

  private get balanceEnabled(): boolean {
    return this.balanceEnabledDraft ?? this.thermostat.heatingAutoBalanceEnabled;
  }

  private clearDrafts(): void {
    this.co2EnabledDraft = undefined;
    this.balanceEnabledDraft = undefined;
  }

  private relayStatesTask = new Task(this, {
    task: async ([,], { signal }) => apiGet<RelayStates>(apiResources.gateways.getRelayStates, { signal }),
    args: () => [this.thermostat?.mode],
  });

  private async handleSave(event: Event): Promise<void> {
    event.preventDefault();

    const data = new FormData(event.target as HTMLFormElement);
    const unit = this.preferences.temperatureUnit;
    const thermostat = this.thermostat;
    const value = (name: string): number => Number(data.get(name));

    // Disabled controls are left out of FormData, so a switched-off section keeps its saved values.
    const co2 = this.co2Enabled;
    const balance = this.balanceEnabled;

    const request: UpdateThermostatConfigurationRequest = {
      hysteresisC: deltaFromPreferredUnit(unit, value('hysteresis')),
      co2RecirculationEnabled: co2,
      co2RecirculationMode: co2 ? (data.get('co2RecirculationMode') as CO2RecirculationMode) : thermostat.co2RecirculationMode,
      co2RecirculationTriggerPpm: co2 ? Math.round(value('co2RecirculationTrigger')) : thermostat.co2RecirculationTriggerPpm,
      co2RecirculationSatisfiedPpm: co2 ? Math.round(value('co2RecirculationSatisfied')) : thermostat.co2RecirculationSatisfiedPpm,
      co2RecirculationDurationMs: co2 ? Math.round(value('co2RecirculationDuration') * MS_PER_MINUTE) : thermostat.co2RecirculationDurationMs,
      heatingAutoBalanceEnabled: balance,
      heatingBalanceTriggerC: balance ? deltaFromPreferredUnit(unit, value('heatingBalanceTrigger')) : thermostat.heatingBalanceTriggerC,
      heatingBalanceSatisfiedC: balance ? deltaFromPreferredUnit(unit, value('heatingBalanceSatisfied')) : thermostat.heatingBalanceSatisfiedC,
      heatingAutoBalanceDurationMs: balance
        ? Math.round(value('heatingAutoBalanceDuration') * MS_PER_MINUTE)
        : thermostat.heatingAutoBalanceDurationMs,
    };

    try {
      await apiPut<void>(apiResources.thermostat.updateThermostatConfiguration, { body: request });
      this.clearDrafts();
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

  private renderForm(): TemplateResult {
    const unit = this.preferences.temperatureUnit;
    const thermostat = this.thermostat;
    const co2Disabled = !this.co2Enabled;
    const balanceDisabled = !this.balanceEnabled;

    return html`
      <form
        @submit=${this.handleSave}
        @reset=${this.clearDrafts}
      >
        <div class="form-group">
          <section class="settings-thermostat__section">
            <h3 class="settings-thermostat__section-title">Hysteresis</h3>
            <p class="settings-thermostat__description">
              How far the temperature may drift from the target before heating or cooling starts and stops.
            </p>
            <div class="form-control">
              <label class="form-control__label">
                Hysteresis (${getPreferredUnit(unit)})
                <app-number-input
                  id="hysteresis"
                  name="hysteresis"
                  step="0.1"
                  class="form-control__input"
                  .min=${convertDeltaToPreferredUnit(unit, MIN_HYSTERESIS_C)}
                  .max=${convertDeltaToPreferredUnit(unit, MAX_HYSTERESIS_C)}
                  .value=${convertDeltaToPreferredUnit(unit, thermostat.hysteresisC)}
                ></app-number-input>
              </label>
            </div>
          </section>

          <section class="settings-thermostat__section">
            <div class="settings-thermostat__section-header">
              <h3 class="settings-thermostat__section-title">CO<sub>2</sub> Recirculation</h3>
              <label class="settings-thermostat__toggle-label">
                Enabled
                <app-toggle
                  name="co2RecirculationEnabled"
                  ?checked=${this.co2Enabled}
                  @change=${(event: Event) => (this.co2EnabledDraft = (event.target as Toggle).checked)}
                ></app-toggle>
              </label>
            </div>
            <p class="settings-thermostat__description">Runs the fan to circulate air when high CO<sub>2</sub> levels are detected.</p>
            <div class="settings-thermostat__grid">
              <div class="form-control">
                <label class="form-control__label">
                  Mode
                  <select
                    id="co2RecirculationMode"
                    name="co2RecirculationMode"
                    class="form-control__input select"
                    aria-label="CO2 recirculation mode"
                    ?disabled=${co2Disabled}
                  >
                    <option
                      value="Average"
                      ?selected=${thermostat.co2RecirculationMode === 'Average'}
                    >
                      Average of all sensors
                    </option>
                    <option
                      value="Peak"
                      ?selected=${thermostat.co2RecirculationMode === 'Peak'}
                    >
                      Highest sensor
                    </option>
                  </select>
                </label>
              </div>
              <div class="form-control">
                <label class="form-control__label">
                  Start above (ppm)
                  <app-number-input
                    id="co2RecirculationTrigger"
                    name="co2RecirculationTrigger"
                    step="50"
                    class="form-control__input"
                    ?disabled=${co2Disabled}
                    .value=${thermostat.co2RecirculationTriggerPpm}
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
                    ?disabled=${co2Disabled}
                    .value=${thermostat.co2RecirculationSatisfiedPpm}
                  ></app-number-input>
                </label>
              </div>
              <div class="form-control">
                <label class="form-control__label">
                  Maximum run time (minutes)
                  <app-number-input
                    id="co2RecirculationDuration"
                    name="co2RecirculationDuration"
                    step="0.5"
                    class="form-control__input"
                    ?disabled=${co2Disabled}
                    .value=${thermostat.co2RecirculationDurationMs / MS_PER_MINUTE}
                  ></app-number-input>
                </label>
              </div>
            </div>
          </section>

          <section class="settings-thermostat__section">
            <div class="settings-thermostat__section-header">
              <h3 class="settings-thermostat__section-title">Heating Balance</h3>
              <label class="settings-thermostat__toggle-label">
                Enabled
                <app-toggle
                  name="heatingAutoBalanceEnabled"
                  ?checked=${this.balanceEnabled}
                  @change=${(event: Event) => (this.balanceEnabledDraft = (event.target as Toggle).checked)}
                ></app-toggle>
              </label>
            </div>
            <p class="settings-thermostat__description">Runs the fan after a heating cycle to even out temperatures when areas differ by too much.</p>
            <div class="settings-thermostat__grid">
              ${this.renderDelta('heatingBalanceTrigger', 'Start when areas differ by more than', thermostat.heatingBalanceTriggerC, balanceDisabled)}
              ${this.renderDelta('heatingBalanceSatisfied', 'Stop when areas differ by', thermostat.heatingBalanceSatisfiedC, balanceDisabled)}
              <div class="form-control">
                <label class="form-control__label">
                  Maximum run time (minutes)
                  <app-number-input
                    id="heatingAutoBalanceDuration"
                    name="heatingAutoBalanceDuration"
                    step="0.5"
                    class="form-control__input"
                    ?disabled=${balanceDisabled}
                    .value=${thermostat.heatingAutoBalanceDurationMs / MS_PER_MINUTE}
                  ></app-number-input>
                </label>
              </div>
            </div>
          </section>
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
          >
            Save
          </button>
        </div>
      </form>
    `;
  }

  override render() {
    return html`
      <section
        class="settings-thermostat"
        role="tabpanel"
        aria-label="Thermostat settings"
      >
        <article class="card">
          ${
            !this.thermostat || !this.preferences
              ? html`<p>Loading thermostat settings...</p>`
              : html`
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
                  ${this.renderForm()}
                `
          }
        </article>
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
