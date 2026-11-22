import '../../../shared/number-input/number-input.js';

import { consume } from '@lit/context';
import { Task } from '@lit/task';
import { html, LitElement, TemplateResult } from 'lit';
import { customElement } from 'lit/decorators.js';

import settingsGatewayStyles from './settings-gateway.styles';
import { defaultRelayStates, relayStatesContext } from '../../../../contexts/control-context';
import { Gateway, RelayStates } from '../../../../models/gateway';
import { apiGet, apiPut } from '../../../../services/api';
import apiResources from '../../../../services/api-resources';
import { dispatchToast } from '../../../../services/utilities.js';
import sharedStyles from '../../../../shared.styles';

@customElement('app-settings-gateway')
export class SettingsGateway extends LitElement {
  static override styles = [sharedStyles, settingsGatewayStyles];

  @consume({ context: relayStatesContext, subscribe: true })
  private relayStates: RelayStates = defaultRelayStates;

  private gatewayTask = new Task(this, {
    task: async (_, { signal }) => apiGet<Gateway>(apiResources.gateways.getGateway, { signal }),
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
    dispatchToast(this, 'Success', 'Gateway settings saved successfully.');
  }

  private renderGateway(gateway: Gateway): TemplateResult {
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
                <app-number-input
                  id="heatingPin"
                  name="heatingPin"
                  step="1"
                  class="form-control__input"
                  .value=${gateway.heatingPin}
                  title="${this.relayStates.heating === true ? 'Heating is currently active, this pin cannot be changed.' : ''}"
                  ?disabled=${gateway.heatingPin != null && this.relayStates.heating === true}
                ></app-number-input>
              </label>
            </div>
            <div class="form-control">
              <label class="form-control__label">
                Cooling Pin
                <app-number-input
                  id="coolingPin"
                  name="coolingPin"
                  step="1"
                  class="form-control__input"
                  .value=${gateway.coolingPin}
                  title="${this.relayStates.cooling === true ? 'Cooling is currently active, this pin cannot be changed.' : ''}"
                  ?disabled=${gateway.coolingPin != null && this.relayStates.cooling === true}
                ></app-number-input>
              </label>
            </div>
            <div class="form-control">
              <label class="form-control__label">
                Fan Pin
                <app-number-input
                  id="fanPin"
                  name="fanPin"
                  step="1"
                  class="form-control__input"
                  .value=${gateway.fanPin}
                  title="${this.relayStates.fan === true ? 'Fan is currently active, this pin cannot be changed.' : ''}"
                  ?disabled=${gateway.fanPin != null && this.relayStates.fan === true}
                ></app-number-input>
              </label>
            </div>
            <div class="form-control">
              <label class="form-control__label">
                Control Pin
                <app-number-input
                  id="controlPin"
                  name="controlPin"
                  step="1"
                  class="form-control__input"
                  .value=${gateway.controlPin}
                  title="${this.relayStates.control === true ? 'Control is currently active, this pin cannot be changed.' : ''}"
                  ?disabled=${gateway.controlPin != null && this.relayStates.control === true}
                ></app-number-input>
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
                <h2 class="card__title">Gateway</h2>
              </div>
              ${this.renderGateway(gateway)}
            `,
            error: () => html`<p class="error">Unable to retrieve gateway settings</p>`,
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
