import '../../../shared/modal/modal.js';
import '../../../shared/tabs/tabs.js';

import { consume } from '@lit/context';
import { html, LitElement, nothing, TemplateResult } from 'lit';
import { customElement, state } from 'lit/decorators.js';
import { unsafeSVG } from 'lit/directives/unsafe-svg.js';
import { when } from 'lit/directives/when.js';

import thermostatEditorStyles from './thermostat-editor.styles.js';
import trashIcon from '../../../../../assets/icons/trash.svg?raw';
import { preferencesContext } from '../../../../contexts/preferences-context.js';
import { thermostatContext } from '../../../../contexts/thermostat-context';
import { Preferences } from '../../../../models/preferences.js';
import { RunType, Schedule, SetPoint, Thermostat } from '../../../../models/thermostat';
import { getPreferredUnit, toPreferredUnit } from '../../../../services/utilities.js';
import sharedStyles from '../../../../shared.styles.js';

@customElement('app-thermostat-editor')
export class ThermostatEditor extends LitElement {
  static override styles = [sharedStyles, thermostatEditorStyles];

  @consume({ context: preferencesContext, subscribe: true })
  preferences!: Preferences;

  @consume({ context: thermostatContext, subscribe: true })
  thermostat!: Thermostat;

  @state()
  private isEditing = true;

  @state()
  private setPoints: SetPoint[] = [];

  @state()
  private schedules: Partial<Schedule>[] = [];

  private get heatingSetPoint(): SetPoint | undefined {
    return this.setPoints.find(sp => sp.type === 'Heating');
  }

  private get heatingSchedules(): Partial<Schedule>[] {
    return this.schedules.filter(sch => sch.type === 'Heating');
  }

  private get coolingSetPoint(): SetPoint | undefined {
    return this.setPoints.find(sp => sp.type === 'Cooling');
  }

  private get coolingSchedules(): Partial<Schedule>[] {
    return this.schedules.filter(sch => sch.type === 'Cooling');
  }

  private get preferredUnit(): string {
    return getPreferredUnit(this.preferences.temperatureUnit);
  }

  open(): void {
    this.isEditing = true;
  }

  private async handleFormSubmit(event: Event): Promise<void> {
    event.preventDefault();
    event.stopPropagation();
  }

  private handleScheduleRemove(index: number): void {
    this.schedules.splice(index, 1);
    this.requestUpdate();
  }

  private renderSchedule(schedule: Partial<Schedule>, index: number): TemplateResult {
    return html`
      <div class="thermostat-editor__schedule">
        <input
          type="hidden"
          name="schedule-type-${index}"
          .value=${schedule.type ?? ''}
        />
        <div class="form-control form-control">
          <label class="form-control__label">
            Start Time
            <input
              id="schedule-start-${index}"
              name="schedule-start-${index}"
              type="time"
              class="form-control__input input"
              .value=${schedule.startTime ?? ''}
            />
          </label>
        </div>
        <div class="form-control form-control">
          <label class="form-control__label">
            End Time
            <input
              id="schedule-end-${index}"
              name="schedule-end-${index}"
              type="time"
              class="form-control__input input"
              .value=${schedule.endTime ?? ''}
            />
          </label>
        </div>
        <div class="form-control form-control">
          <label class="form-control__label">
            Temperature
            <input
              id="schedule-target-${index}"
              name="schedule-target-${index}"
              type="number"
              class="form-control__input input"
              placeholder="${this.preferredUnit}"
              .value=${toPreferredUnit(this.preferences.temperatureUnit, schedule.targetTemperatureC)}
            />
          </label>
        </div>
        <div class="thermostat-editor__schedule-actions">
          <button
            type="button"
            class="button button--icon button--danger"
            aria-label="Remove Schedule"
            title="Remove Schedule"
            @click=${() => this.handleScheduleRemove(index)}
          >
            ${unsafeSVG(trashIcon)}
          </button>
        </div>
      </div>
    `;
  }

  private renderSchedules(runType: RunType, schedules: Partial<Schedule>[]): TemplateResult {
    return html`
      <p class="thermostat-editor__panel-description">Create ${runType} schedules for the thermostat.</p>
      <div class="thermostat-editor__schedules">
        <div class="thermostat-editor__schedule-list">${schedules.map((schedule, index) => this.renderSchedule(schedule, index))}</div>
        <button
          type="button"
          class="button button-secondary thermostat-editor__add-schedule"
          @click=${() => {
            this.schedules.push({
              type: runType,
            });
            this.requestUpdate();
          }}
        >
          Add Schedule Block
        </button>
      </div>
    `;
  }

  private renderForecastLockoutPanel(): TemplateResult {
    return html`
      <p class="thermostat-editor__panel-description">
        Locks out HVAC operation based on the outdoor temperature forecast. If the outdoor temperature is forecasted to be above the lockout
        temperature, the thermostat will not allow heating or cooling to operate.
      </p>

      ${when(
        this.thermostat.mode === 'Heating' || this.thermostat.mode === 'Automatic',
        () => html`
          <div class="form-control form-control">
            <label class="form-control__label">
              Lockout Heating if outdoor temp ≥
              <input
                type="number"
                id="heating-lockout"
                name="heating-lockout"
                class="form-control__input input"
                placeholder="${this.preferredUnit}"
                .value=${this.heatingSetPoint?.activationTemperatureC?.toString() ?? ''}
              />
            </label>
          </div>
        `,
      )}
      ${when(
        this.thermostat.mode === 'Cooling' || this.thermostat.mode === 'Automatic',
        () => html`
          <div class="form-control form-control">
            <label class="form-control__label">
              Lockout Cooling if outdoor temp ≤
              <input
                type="number"
                id="cooling-lockout"
                name="cooling-lockout"
                class="form-control__input input"
                placeholder="${this.preferredUnit}"
                .value=${this.coolingSetPoint?.activationTemperatureC?.toString() ?? ''}
              />
            </label>
          </div>
        `,
      )}
    `;
  }

  private renderSetPointPanel(): TemplateResult {
    return html`
      <p class="thermostat-editor__panel-description">Set Points are used when no schedule is active.</p>
      ${when(
        this.thermostat.mode === 'Heating' || this.thermostat.mode === 'Automatic',
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
                .value=${this.heatingSetPoint?.targetTemperatureC?.toString() ?? ''}
              />
            </label>
          </div>
        `,
      )}
      ${when(
        this.thermostat.mode === 'Cooling' || this.thermostat.mode === 'Automatic',
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
                .value=${this.coolingSetPoint?.targetTemperatureC?.toString() ?? ''}
              />
            </label>
          </div>
        `,
      )}
    `;
  }

  override render(): TemplateResult | typeof nothing {
    if (!this.thermostat) return nothing;

    return html`
      <app-modal
        ?open=${this.isEditing}
        .heading="${'Set Points & Schedules'}"
        @close=${() => (this.isEditing = false)}
      >
        <form
          class="thermostat-editor form-group"
          @submit=${this.handleFormSubmit}
        >
          <app-tabs description="Manage the thermostat's set points and schedules.">
            <button
              id="set-points-tab"
              slot="tab"
            >
              Set Points
            </button>
            <button
              id="forecast-lockout-tab"
              slot="tab"
            >
              Forecast
            </button>
            ${when(
              this.thermostat.mode === 'Heating' || this.thermostat.mode === 'Automatic',
              () => html`
                <button
                  id="heating-schedules-tab"
                  slot="tab"
                >
                  Heat Schedules
                </button>
              `,
            )}
            ${when(
              this.thermostat.mode === 'Cooling' || this.thermostat.mode === 'Automatic',
              () => html`
                <button
                  id="cooling-schedules-tab"
                  slot="tab"
                >
                  Cool Schedules
                </button>
              `,
            )}
            <div
              id="set-points-panel"
              slot="panel"
              class="thermostat-editor__panel"
            >
              ${this.renderSetPointPanel()}
            </div>
            <div
              id="forecast-lockout-panel"
              slot="panel"
              class="thermostat-editor__panel"
            >
              ${this.renderForecastLockoutPanel()}
            </div>
            ${when(
              this.thermostat.mode === 'Heating' || this.thermostat.mode === 'Automatic',
              () => html`
                <div
                  id="heating-schedules-panel"
                  slot="panel"
                  class="thermostat-editor__panel"
                >
                  ${this.renderSchedules('Heating', this.heatingSchedules)}
                </div>
              `,
            )}
            ${when(
              this.thermostat.mode === 'Cooling' || this.thermostat.mode === 'Automatic',
              () => html`
                <div
                  id="cooling-schedules-panel"
                  slot="panel"
                  class="thermostat-editor__panel"
                >
                  ${this.renderSchedules('Cooling', this.coolingSchedules)}
                </div>
              `,
            )}
          </app-tabs>
        </form>
      </app-modal>
    `;
  }
}
