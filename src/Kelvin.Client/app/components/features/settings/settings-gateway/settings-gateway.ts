import '../../../shared/toggle/toggle.js';

import { Task } from '@lit/task';
import { html, LitElement, TemplateResult } from 'lit';
import { customElement, query } from 'lit/decorators.js';

import settingsGatewayStyles from './settings-gateway.styles';
import { Gateway, RelayStates } from '../../../../models/gateway';
import { apiGet, apiPut } from '../../../../services/api';
import apiResources from '../../../../services/api-resources';
import { dispatchToast } from '../../../../services/utilities.js';
import sharedStyles from '../../../../shared.styles';
import { confirmModal } from '../../../shared/modal/modal-utilities.js';
import { Toggle } from '../../../shared/toggle/toggle.js';

@customElement('app-settings-gateway')
export class SettingsGateway extends LitElement {
  static override styles = [sharedStyles, settingsGatewayStyles];

  @query('app-toggle')
  private toggle!: Toggle;

  private gatewayTask = new Task(this, {
    task: async (_, { signal }) => {
      const gateway = await apiGet<Gateway>(apiResources.gateways.getGateway, { signal });
      const relayStates = await apiGet<RelayStates>(apiResources.gateways.getRelayStates, { signal });

      return {
        ...gateway,
        relayStates,
      };
    },
    args: () => [],
  });

  private async handleSave(event: Event): Promise<void> {
    event.preventDefault();

    const form = event.target as HTMLFormElement;
    const formData = new FormData(form);
    const heatingPin = formData.get('heatingPin') as string;
    const coolingPin = formData.get('coolingPin') as string;
    const fanPin = formData.get('fanPin') as string;
    const controlPin = formData.get('controlPin') as string;
    const update = {
      heatingPin: heatingPin ? parseInt(heatingPin, 10) : null,
      coolingPin: coolingPin ? parseInt(coolingPin, 10) : null,
      fanPin: fanPin ? parseInt(fanPin, 10) : null,
      controlPin: controlPin ? parseInt(controlPin, 10) : null,
    };

    await apiPut(apiResources.gateways.updateGateway, { body: update });
    dispatchToast(this, 'success', 'Gateway settings saved successfully.');
  }

  private async handleControlChange(): Promise<void> {
    if (this.toggle.checked) {
      const result = await confirmModal('Enabling the gateway will allow the system to control your HVAC. Are you sure you want to enable it?');
      if (!result) {
        this.toggle.checked = false;
        return;
      }
    } else {
      const result = await confirmModal(
        `
          Disabling the gateway will prevent the system from controlling your HVAC and revert control to the failsafe thermostat. 
          Are you sure you want to disable it?
        `,
      );

      if (!result) {
        this.toggle.checked = true;
        return;
      }
    }
  }

  private renderGateway(gateway: Gateway & { relayStates: RelayStates }): TemplateResult {
    return html`
      <form @submit=${this.handleSave}>
        <div class="form-group">
          <div class="form-control">
            <label class="form-control__label">
              MAC Address
              <input
                id="macAddress"
                name="macAddress"
                readonly
                class="form-control__input input"
                type="text"
                .value=${gateway.macAddress}
              />
            </label>
          </div>
          <div class="settings-gateway__pins">
            <div class="form-control">
              <label class="form-control__label">
                Heating Pin
                <input
                  id="heatingPin"
                  name="heatingPin"
                  class="form-control__input input"
                  type="number"
                  title="${gateway.relayStates?.heating === true ? 'Heating is currently active, this pin cannot be changed.' : ''}"
                  ?disabled=${gateway.heatingPin != null && gateway.relayStates?.heating === true}
                  .value=${gateway.heatingPin?.toString() ?? ''}
                />
              </label>
            </div>
            <div class="form-control">
              <label class="form-control__label">
                Cooling Pin
                <input
                  id="coolingPin"
                  name="coolingPin"
                  class="form-control__input input"
                  type="number"
                  title="${gateway.relayStates?.cooling === true ? 'Cooling is currently active, this pin cannot be changed.' : ''}"
                  ?disabled=${gateway.coolingPin != null && gateway.relayStates?.cooling === true}
                  .value=${gateway.coolingPin?.toString() ?? ''}
                />
              </label>
            </div>
            <div class="form-control">
              <label class="form-control__label">
                Fan Pin
                <input
                  id="fanPin"
                  name="fanPin"
                  class="form-control__input input"
                  type="number"
                  title="${gateway.relayStates?.fan === true ? 'Fan is currently active, this pin cannot be changed.' : ''}"
                  ?disabled=${gateway.fanPin != null && gateway.relayStates?.fan === true}
                  .value=${gateway.fanPin?.toString() ?? ''}
                />
              </label>
            </div>
            <div class="form-control">
              <label class="form-control__label">
                Control Pin
                <input
                  id="controlPin"
                  name="controlPin"
                  class="form-control__input input"
                  type="number"
                  title="${gateway.relayStates?.control === true ? 'Control is currently active, this pin cannot be changed.' : ''}"
                  ?disabled=${gateway.controlPin != null && gateway.relayStates?.control === true}
                  .value=${gateway.controlPin?.toString() ?? ''}
                />
              </label>
            </div>
          </div>
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
        class="settings-gateway"
        role="tabpanel"
        aria-label="Gateway settings"
      >
        <article class="card">
          ${this.gatewayTask.render({
            pending: () => html`<p>Loading gateway settings...</p>`,
            complete: gateway => html`
              <div class="card__header">
                <h2 class="card__title">Gateway Settings</h2>
                <label class="settings-gateway__toggle-label">
                  HVAC Control
                  <app-toggle
                    @change=${this.handleControlChange}
                    ?checked=${gateway.relayStates?.control ?? false}
                  ></app-toggle>
                </label>
              </div>
              ${this.renderGateway(gateway)}
            `,
            error: error => html`<p>Error loading gateway settings: ${error instanceof Error ? error.message : String(error)}</p>`,
          })}
        </article>
      </section>
    `;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions
  interface HTMLElementTagNameMap {
    'settings-gateway': SettingsGateway;
  }
}
