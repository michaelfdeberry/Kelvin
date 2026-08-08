import '../../../shared/modal/modal.js';
import '../../../shared/tabs/tabs.js';
import './thermostat-editor-forecast.js';
import './thermostat-editor-schedules.js';
import './thermostat-editor-set-points.js';

import { consume } from '@lit/context';
import { html, LitElement, nothing, TemplateResult } from 'lit';
import { customElement, query, queryAll } from 'lit/decorators.js';
import { when } from 'lit/directives/when.js';

import { ThermostatEditorForecast } from './thermostat-editor-forecast.js';
import { ThermostatEditorSchedules } from './thermostat-editor-schedules.js';
import { ThermostatEditorSetPoints } from './thermostat-editor-set-points.js';
import thermostatEditorStyles from './thermostat-editor.styles.js';
import { thermostatContext } from '../../../../contexts/thermostat-context.js';
import { events } from '../../../../events.js';
import { Thermostat, UpdateThermostatSettingsRequest } from '../../../../models/thermostat.js';
import resources from '../../../../services/api-resources.js';
import { apiPut } from '../../../../services/api.js';
import { dispatchCustomEvent, dispatchToast } from '../../../../services/utilities.js';
import sharedStyles from '../../../../shared.styles.js';
import { Modal } from '../../../shared/modal/modal.js';

@customElement('app-thermostat-editor')
export class ThermostatEditor extends LitElement {
  static override styles = [sharedStyles, thermostatEditorStyles];

  @query('app-modal')
  private modal!: Modal;

  @query('app-thermostat-editor-set-points')
  private setPointsPanel!: ThermostatEditorSetPoints;

  @query('app-thermostat-editor-forecast')
  private forecastPanel!: ThermostatEditorForecast;

  @queryAll('app-thermostat-editor-schedules')
  private schedulesPanels!: NodeListOf<ThermostatEditorSchedules>;

  @consume({ context: thermostatContext, subscribe: true })
  thermostat!: Thermostat;

  private get isHeatingAvailable(): boolean {
    return this.thermostat.mode === 'Heating' || this.thermostat.mode === 'Automatic';
  }

  private get isCoolingAvailable(): boolean {
    return this.thermostat.mode === 'Cooling' || this.thermostat.mode === 'Automatic';
  }

  open(): void {
    this.schedulesPanels.forEach(panel => panel.resetSchedules());
    this.modal.show();
  }

  private async handleFormSubmit(event: Event): Promise<void> {
    event.preventDefault();

    const { heatingLockoutC, coolingLockoutC } = this.forecastPanel.getLockouts();
    const request: UpdateThermostatSettingsRequest = {
      heatingLockoutC,
      coolingLockoutC,
      setPoints: this.setPointsPanel.getSetPoints(),
      schedules: Array.from(this.schedulesPanels).flatMap(panel => panel.getSchedules()),
    };

    try {
      await apiPut<void>(resources.thermostat.updateThermostatSettings, { body: request });
      dispatchCustomEvent(this, events.thermostatUpdated);
      dispatchToast(this, 'success', 'Thermostat settings saved successfully.');
      this.modal.hide();
    } catch {
      // apiPut already surfaces an error toast - keep the modal open so the user can correct the input.
    }
  }

  override render(): TemplateResult | typeof nothing {
    if (!this.thermostat) return nothing;

    return html`
      <app-modal .heading="${'Set Points & Schedules'}">
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
              Forecast</button
            ><button
              id="heating-schedules-tab"
              slot="tab"
              ?hidden=${!this.isHeatingAvailable}
            >
              Heat Schedules
            </button>
            <button
              id="cooling-schedules-tab"
              slot="tab"
              ?hidden=${!this.isCoolingAvailable}
            >
              Cool Schedules
            </button>
            <app-thermostat-editor-set-points
              id="set-points-panel"
              slot="panel"
            ></app-thermostat-editor-set-points>
            <app-thermostat-editor-forecast
              id="forecast-lockout-panel"
              slot="panel"
            ></app-thermostat-editor-forecast>
            ${when(
              this.isHeatingAvailable,
              () => html`
                <app-thermostat-editor-schedules
                  id="heating-schedules-panel"
                  slot="panel"
                  .runType=${'Heating'}
                ></app-thermostat-editor-schedules>
              `,
            )}
            ${when(
              this.isCoolingAvailable,
              () => html`
                <app-thermostat-editor-schedules
                  id="cooling-schedules-panel"
                  slot="panel"
                  .runType=${'Cooling'}
                ></app-thermostat-editor-schedules>
              `,
            )}
          </app-tabs>
        </form>
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
          @click=${this.handleFormSubmit}
        >
          Save
        </button>
      </app-modal>
    `;
  }
}
