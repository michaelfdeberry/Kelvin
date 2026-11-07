import '../../../shared/modal/modal.js';
import '../../../shared/number-input/number-input.js';

import { consume } from '@lit/context';
import { html, LitElement, TemplateResult } from 'lit';
import { customElement, query, state } from 'lit/decorators.js';
import { keyed } from 'lit/directives/keyed.js';
import { unsafeSVG } from 'lit/directives/unsafe-svg.js';

import humidifierEditorStyles from './humidifier-editor.styles.js';
import trashIcon from '../../../../../assets/icons/trash.svg?raw';
import { preferencesContext } from '../../../../contexts/preferences-context.js';
import { events } from '../../../../events.js';
import { Humidifier, HumidityTarget, UpdateHumidifierConfigurationRequest } from '../../../../models/devices.js';
import { Preferences } from '../../../../models/preferences.js';
import apiResources from '../../../../services/api-resources.js';
import { apiPut } from '../../../../services/api.js';
import { convertToPreferredUnit, dispatchCustomEvent, dispatchToast, fromPreferredUnit, getPreferredUnit } from '../../../../services/utilities.js';
import sharedStyles from '../../../../shared.styles.js';
import { Modal } from '../../../shared/modal/modal.js';
import { NumberInput } from '../../../shared/number-input/number-input.js';

export const MONTHS = Array.from({ length: 12 }, (_, index) => new Intl.DateTimeFormat('en', { month: 'long' }).format(new Date(2000, index, 1)));

@customElement('app-humidifier-editor')
export class HumidifierEditor extends LitElement {
  static override styles = [sharedStyles, humidifierEditorStyles];

  @consume({ context: preferencesContext, subscribe: true })
  private preferences?: Preferences;

  @query('app-modal')
  private modal!: Modal;

  @state()
  private humidifier?: Humidifier;

  @state()
  private rows: HumidityTarget[] = [];

  // Bumping this remounts the inputs so they drop any unsaved value.
  @state()
  private revision = 0;

  open(humidifier: Humidifier): void {
    this.humidifier = humidifier;
    this.rows = humidifier.targets.map(target => ({ ...target }));
    this.revision++;
    this.modal.show();
  }

  // Rows come from the inputs themselves because a blank input contributes nothing to FormData.
  private readRows(): HumidityTarget[] | undefined {
    const unit = this.preferences!.temperatureUnit;
    const targets: HumidityTarget[] = [];

    for (const row of this.renderRoot.querySelectorAll('.humidifier-editor__row')) {
      const outdoor = row.querySelector<NumberInput>('[data-field="outdoor"]')?.value;
      const humidity = row.querySelector<NumberInput>('[data-field="humidity"]')?.value;
      if (outdoor === undefined || humidity === undefined || !Number.isFinite(outdoor) || !Number.isFinite(humidity)) return undefined;

      targets.push({ outdoorTemperatureC: Math.round(fromPreferredUnit(unit, Math.round(outdoor)) * 10) / 10, humidityPercentage: humidity });
    }

    return targets;
  }

  private handleAdd(): void {
    const rows = this.readRows() ?? this.rows;
    const warmest = rows.reduce((max, row) => Math.max(max, row.outdoorTemperatureC), -Infinity);

    this.rows = [...rows, { outdoorTemperatureC: Number.isFinite(warmest) ? Math.round(warmest) + 5 : 0, humidityPercentage: 40 }];
    this.revision++;
  }

  private handleRemove(index: number): void {
    const rows = this.readRows() ?? this.rows;

    this.rows = rows.filter((_, rowIndex) => rowIndex !== index);
    this.revision++;
  }

  // Outdoor temperatures are whole numbers; snap typed decimals once the user leaves the field.
  private handleOutdoorBlur(event: Event): void {
    const input = event.currentTarget as NumberInput;
    if (input.value !== undefined && Number.isFinite(input.value)) input.value = Math.round(input.value);
  }

  private async handleSave(event: Event): Promise<void> {
    event.preventDefault();

    const form = this.renderRoot.querySelector('form')!;
    const data = new FormData(form);
    const targets = this.readRows();
    if (!targets) {
      dispatchToast(this, 'Error', 'Every target needs an outdoor temperature and a humidity.');
      return;
    }

    if (new Set(targets.map(target => target.outdoorTemperatureC)).size !== targets.length) {
      dispatchToast(this, 'Error', 'Each target needs a different outdoor temperature.');
      return;
    }

    const body: UpdateHumidifierConfigurationRequest = {
      startMonth: Number(data.get('startMonth')),
      endMonth: Number(data.get('endMonth')),
      targets,
    };

    try {
      await apiPut<void>(apiResources.humidifiers.updateHumidifierConfiguration, { body, routeParams: { id: this.humidifier!.id } });
      dispatchCustomEvent(this, events.devicesUpdated);
      dispatchToast(this, 'Success', 'Humidifier settings saved successfully.');
      this.modal.hide();
    } catch {
      // apiPut already surfaces an error toast - keep the modal open so the user can correct the input.
    }
  }

  private renderMonth(name: string, label: string, selected: number): TemplateResult {
    return html`
      <div class="form-control">
        <label class="form-control__label">
          ${label}
          <select
            name=${name}
            class="form-control__input select"
          >
            ${MONTHS.map(
              (month, index) => html`
                <option
                  value=${index + 1}
                  ?selected=${index + 1 === selected}
                >
                  ${month}
                </option>
              `,
            )}
          </select>
        </label>
      </div>
    `;
  }

  private renderRow(target: HumidityTarget, index: number): TemplateResult {
    const unit = this.preferences!.temperatureUnit;

    return html`
      <div class="humidifier-editor__row">
        <div class="form-control">
          <label class="form-control__label">
            Outdoor Temperature (${getPreferredUnit(unit)})
            <app-number-input
              data-field="outdoor"
              step="1"
              class="form-control__input"
              .value=${Math.round(convertToPreferredUnit(unit, target.outdoorTemperatureC)!)}
              @focusout=${this.handleOutdoorBlur}
            ></app-number-input>
          </label>
        </div>
        <div class="form-control">
          <label class="form-control__label">
            Target humidity (%)
            <app-number-input
              data-field="humidity"
              step="1"
              min="0"
              max="100"
              class="form-control__input"
              .value=${target.humidityPercentage}
            ></app-number-input>
          </label>
        </div>
        <div class="humidifier-editor__row-actions">
          <button
            type="button"
            class="button button--icon button--danger"
            aria-label="Remove Target"
            title="Remove Target"
            @click=${() => this.handleRemove(index)}
          >
            ${unsafeSVG(trashIcon)}
          </button>
        </div>
      </div>
    `;
  }

  override render(): TemplateResult {
    return html`
      <app-modal .heading=${'Humidifier Settings'}>
        ${
          this.humidifier && this.preferences
            ? keyed(
                this.revision,
                html`
                  <form
                    class="humidifier-editor"
                    @submit=${this.handleSave}
                  >
                    <p class="humidifier-editor__description">
                      The humidifier runs during its season while the furnace is heating and the indoor humidity is below the target for the current
                      outdoor temperature.
                    </p>
                    <div class="humidifier-editor__season">
                      ${this.renderMonth('startMonth', 'Season starts', this.humidifier.startMonth)}
                      ${this.renderMonth('endMonth', 'Season ends', this.humidifier.endMonth)}
                    </div>
                    <div class="humidifier-editor__list">${this.rows.map((target, index) => this.renderRow(target, index))}</div>
                    <button
                      type="button"
                      class="button button-secondary humidifier-editor__add"
                      @click=${this.handleAdd}
                    >
                      Add Target
                    </button>
                  </form>
                `,
              )
            : ''
        }
        <button
          class="button button--secondary"
          slot="actions"
          @click=${() => this.modal.hide('close-button')}
        >
          Cancel
        </button>
        <button
          class="button button--primary"
          slot="actions"
          @click=${this.handleSave}
        >
          Save
        </button>
      </app-modal>
    `;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-humidifier-editor': HumidifierEditor;
  }
}
