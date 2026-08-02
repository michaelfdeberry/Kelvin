import '../../../shared/modal/modal.js';

import { consume } from '@lit/context';
import { LitElement, TemplateResult, html, nothing } from 'lit';
import { customElement, property, query, state } from 'lit/decorators.js';

import sensorSettingsStyles from './sensor-settings.styles.js';
import { sensorsContext } from '../../../../contexts/sensors-context.js';
import { events } from '../../../../events.js';
import { Sensor } from '../../../../models/sensors.js';
import { apiFetch } from '../../../../services/api.js';
import { dispatchCustomEvent, dispatchToast } from '../../../../services/utilities.js';
import sharedStyles from '../../../../shared.styles.js';
import { Modal } from '../../../shared/modal/modal.js';
import { when } from 'lit/directives/when.js';

@customElement('app-sensor-settings')
export class SensorSettings extends LitElement {
  static override styles = [sharedStyles, sensorSettingsStyles];

  @consume({ context: sensorsContext, subscribe: true })
  sensors!: Sensor[];

  @property({ type: String })
  sensorId = '';

  @query('app-modal')
  private modal!: Modal;

  @query('form')
  private form!: HTMLFormElement;

  @state()
  private isModalOpen = false;

  public open() {
    this.isModalOpen = true;
  }

  public close() {
    this.isModalOpen = false;
  }

  private get sensor(): Sensor | undefined {
    return this.sensors.find(sensor => sensor.id === this.sensorId);
  }

  private async saveChanges(event: Event): Promise<void> {
    event.preventDefault();

    const formData = new FormData(this.form);
    const sensorName = formData.get('sensor-name') as string;
    const hasHumiditySensor = formData.get('has-humidity-sensor') === 'on';
    const hasCO2Sensor = formData.get('has-co2-sensor') === 'on';
    const hasBattery = formData.get('has-battery') === 'on';

    if (!sensorName) {
      dispatchToast(this, 'error', 'Sensor name is required.', { duration: 3000 });
      return;
    }

    const updatedSensor: Sensor = {
      ...this.sensor!,
      name: sensorName,
      hasHumiditySensor,
      hasCO2Sensor,
      hasBattery,
    };

    await apiFetch(`sensors/${this.sensor?.id}`, {
      method: 'PUT',
      body: JSON.stringify(updatedSensor),
    });

    dispatchToast(this, 'success', 'Sensor updated successfully.', { duration: 3000 });
    dispatchCustomEvent(this, events.sensorsUpdated);

    this.isModalOpen = false;
  }

  override render(): TemplateResult | typeof nothing {
    if (!this.sensor) return nothing;

    return html`
      <app-modal
        ?open=${this.isModalOpen}
        heading="Edit Sensor Configuration"
        description="Edit the configuration for the sensor with ID: ${this.sensorId}"
        @modal-closed=${() => (this.isModalOpen = false)}
      >
        ${when(
          this.isModalOpen,
          () => html`
            <form
              class="form-group"
              @submit=${this.saveChanges}
            >
              <div class="form-control">
                <label
                  for="sensor-name"
                  class="form-control__label"
                >
                  Sensor Name
                  <input
                    type="text"
                    id="sensor-name"
                    name="sensor-name"
                    class="form-control__input input"
                    placeholder="E.g. Living Room, Primary Bedroom, etc."
                    .value=${this.sensor?.name ?? ''}
                  />
                </label>
              </div>
              <fieldset>
                <legend>Sensor Capabilities</legend>
                <div class="form-control">
                  <label
                    for="has-humidity-sensor"
                    class="form-control__label"
                  >
                    <input
                      type="checkbox"
                      id="has-humidity-sensor"
                      name="has-humidity-sensor"
                      class="form-control__input checkbox"
                      ?checked=${this.sensor?.hasHumiditySensor}
                    />
                    Has Humidity Sensor
                  </label>
                </div>
                <div class="form-control">
                  <label
                    for="has-co2-sensor"
                    class="form-control__label"
                  >
                    <input
                      type="checkbox"
                      id="has-co2-sensor"
                      name="has-co2-sensor"
                      class="form-control__input checkbox"
                      ?checked=${this.sensor?.hasCO2Sensor}
                    />
                    Has CO2 Sensor
                  </label>
                </div>
                <div class="form-control">
                  <label
                    for="has-battery"
                    class="form-control__label"
                  >
                    <input
                      type="checkbox"
                      id="has-battery"
                      name="has-battery"
                      class="form-control__input checkbox"
                      ?checked=${this.sensor?.hasBattery}
                    />
                    Has Battery
                  </label>
                </div>
              </fieldset>
            </form>
          `,
        )}
        <div slot="actions">
          <button
            type="button"
            class="button button--secondary"
            @click=${() => this.modal.close('close-button')}
          >
            Cancel
          </button>
          <button
            type="submit"
            class="button button--primary"
            @click=${this.saveChanges}
          >
            Save
          </button>
        </div>
      </app-modal>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'app-sensor-settings': SensorSettings;
  }
}
