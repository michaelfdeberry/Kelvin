import { consume } from '@lit/context';
import { html, LitElement, TemplateResult } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';
import { unsafeSVG } from 'lit/directives/unsafe-svg.js';

import thermostatEditorSchedulesStyles from './thermostat-editor-schedules.styles.js';
import trashIcon from '../../../../../assets/icons/trash.svg?raw';
import { preferencesContext } from '../../../../contexts/preferences-context.js';
import { schedulesContext } from '../../../../contexts/thermostat-context.js';
import { Preferences } from '../../../../models/preferences.js';
import { RunType, Schedule, ScheduleInput } from '../../../../models/thermostat.js';
import { fromPreferredUnit, getPreferredUnit, toPreferredUnit } from '../../../../services/utilities.js';
import sharedStyles from '../../../../shared.styles.js';

@customElement('app-thermostat-editor-schedules')
export class ThermostatEditorSchedules extends LitElement {
  static override styles = [sharedStyles, thermostatEditorSchedulesStyles];

  @consume({ context: preferencesContext, subscribe: true })
  preferences!: Preferences;

  @consume({ context: schedulesContext, subscribe: true })
  allSchedules!: Schedule[];

  @property({ attribute: 'run-type' })
  runType!: RunType;

  @state()
  private schedules: Partial<Schedule>[] = [];

  override connectedCallback(): void {
    super.connectedCallback();
    this.resetSchedules();
  }

  private get preferredUnit(): string {
    return getPreferredUnit(this.preferences.temperatureUnit);
  }

  resetSchedules(): void {
    this.schedules = this.allSchedules.filter(schedule => schedule.type === this.runType).map(schedule => ({ ...schedule }));
  }

  getSchedules(): ScheduleInput[] {
    const rows = Array.from(this.shadowRoot?.querySelectorAll<HTMLElement>('.thermostat-editor-schedules__schedule') ?? []);

    return this.schedules.map((schedule, index) => {
      const row = rows[index];
      const startInput = row?.querySelector<HTMLInputElement>('.thermostat-editor-schedules__start-input');
      const endInput = row?.querySelector<HTMLInputElement>('.thermostat-editor-schedules__end-input');
      const targetInput = row?.querySelector<HTMLInputElement>('.thermostat-editor-schedules__target-input');

      return {
        id: schedule.id,
        type: this.runType,
        startTime: normalizeTime(startInput?.value ?? ''),
        endTime: normalizeTime(endInput?.value ?? ''),
        targetTemperatureC: fromPreferredUnit(this.preferences.temperatureUnit, Number(targetInput?.value ?? 0)),
      };
    });
  }

  private handleScheduleRemove(index: number): void {
    this.schedules.splice(index, 1);
    this.requestUpdate();
  }

  private renderSchedule(schedule: Partial<Schedule>, index: number): TemplateResult {
    return html`
      <div class="thermostat-editor-schedules__schedule">
        <div class="form-control form-control">
          <label class="form-control__label">
            Start Time
            <input
              id="schedule-start-${index}"
              type="time"
              class="form-control__input input thermostat-editor-schedules__start-input"
              .value=${schedule.startTime ?? ''}
            />
          </label>
        </div>
        <div class="form-control form-control">
          <label class="form-control__label">
            End Time
            <input
              id="schedule-end-${index}"
              type="time"
              class="form-control__input input thermostat-editor-schedules__end-input"
              .value=${schedule.endTime ?? ''}
            />
          </label>
        </div>
        <div class="form-control form-control">
          <label class="form-control__label">
            Temperature
            <input
              id="schedule-target-${index}"
              type="number"
              class="form-control__input input thermostat-editor-schedules__target-input"
              placeholder="${this.preferredUnit}"
              .value=${toPreferredUnit(this.preferences.temperatureUnit, schedule.targetTemperatureC)}
            />
          </label>
        </div>
        <div class="thermostat-editor-schedules__schedule-actions">
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

  override render(): TemplateResult {
    return html`
      <p class="thermostat-editor-schedules__description">Create ${this.runType} schedules for the thermostat.</p>
      <div class="thermostat-editor-schedules__list">${this.schedules.map((schedule, index) => this.renderSchedule(schedule, index))}</div>
      <button
        type="button"
        class="button button-secondary thermostat-editor-schedules__add"
        @click=${() => {
          this.schedules.push({
            type: this.runType,
          });
          this.requestUpdate();
        }}
      >
        Add Schedule Block
      </button>
    `;
  }
}

function normalizeTime(value: string): string {
  return value.length === 5 ? `${value}:00` : value;
}
