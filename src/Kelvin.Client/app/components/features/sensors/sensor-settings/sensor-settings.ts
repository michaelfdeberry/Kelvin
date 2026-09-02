import '../../../shared/modal/modal.js';
import '../../../shared/number-input/number-input.js';
import '../../../shared/tabs/tabs.js';

import { consume } from '@lit/context';
import { LitElement, TemplateResult, html, nothing } from 'lit';
import { customElement, property, query, state } from 'lit/decorators.js';
import { when } from 'lit/directives/when.js';

import sensorSettingsStyles from './sensor-settings.styles.js';
import { sensorsContext } from '../../../../contexts/sensors-context.js';
import { events } from '../../../../events.js';
import { Sensor } from '../../../../models/sensors.js';
import resources from '../../../../services/api-resources.js';
import { apiPut } from '../../../../services/api.js';
import { dispatchCustomEvent, dispatchToast } from '../../../../services/utilities.js';
import sharedStyles from '../../../../shared.styles.js';
import { Modal } from '../../../shared/modal/modal.js';

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

  @state()
  private hasHumiditySensor = false;

  @state()
  private hasCO2Sensor = false;

  override updated(changes: Map<string | number | symbol, unknown>): void {
    if (changes.has('sensorId')) {
      this.hasHumiditySensor = this.sensor?.hasHumiditySensor ?? false;
      this.hasCO2Sensor = this.sensor?.hasCO2Sensor ?? false;
    }
  }

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
    const temperatureOffset = parseFloat(formData.get('temperature-offset') as string) || 0;
    const humidityOffset = parseFloat(formData.get('humidity-offset') as string) || 0;
    const co2Offset = parseFloat(formData.get('co2-offset') as string) || 0;

    if (!sensorName) {
      dispatchToast(this, 'Error', 'Sensor name is required.', { duration: 3000 });
      return;
    }

    const updatedSensor: Sensor = {
      ...this.sensor!,
      name: sensorName,
      hasHumiditySensor,
      hasCO2Sensor,
      hasBattery,
      temperatureCOffset: temperatureOffset,
      cO2LevelPpmOffset: co2Offset,
      humidityPercentageOffset: humidityOffset,
    };

    await apiPut<void>(resources.sensors.updateSensor, {
      body: updatedSensor,
      routeParams: { id: this.sensor!.id },
    });

    dispatchToast(this, 'Success', 'Sensor updated successfully.', { duration: 3000 });
    dispatchCustomEvent(this, events.sensorsUpdated);

    this.isModalOpen = false;
  }

  override render(): TemplateResult | typeof nothing {
    if (!this.sensor) return nothing;

    return html`
      <app-modal
        tabs
        ?open=${this.isModalOpen}
        heading="Edit Sensor Configuration"
        description="Edit the configuration for the sensor with ID: ${this.sensorId}"
        @modal-closed=${() => (this.isModalOpen = false)}
      >
        ${when(
          this.isModalOpen,
          () => html`
            <form
              class="sensor-settings form-group"
              @submit=${this.saveChanges}
            >
              <app-tabs>
                <button
                  id="set-points-tab"
                  slot="tab"
                >
                  Settings
                </button>
                <button
                  id="offsets-tab"
                  slot="tab"
                >
                  Offsets
                </button>
                <div slot="panel">
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
                          @change=${(e: Event) => (this.hasHumiditySensor = (e.target as HTMLInputElement).checked)}
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
                          @change=${(e: Event) => (this.hasCO2Sensor = (e.target as HTMLInputElement).checked)}
                        />
                        <span>Has CO<sub>2</sub> Sensor</span>
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
                </div>

                <div slot="panel">
                  <div class="form-control">
                    <label
                      for="temperature-offset"
                      class="form-control__label"
                    >
                      Temperature Offset
                      <app-number-input
                        class="form-control__input"
                        id="temperature-offset"
                        name="temperature-offset"
                        step="0.1"
                        .value=${this.sensor?.temperatureCOffset ?? 0}
                      ></app-number-input>
                    </label>
                  </div>
                  ${when(
                    this.hasHumiditySensor,
                    () => html`
                      <div class="form-control">
                        <label
                          for="humidity-offset"
                          class="form-control__label"
                        >
                          Humidity Offset
                          <app-number-input
                            class="form-control__input"
                            id="humidity-offset"
                            name="humidity-offset"
                            step="0.1"
                            .value=${this.sensor?.humidityPercentageOffset ?? 0}
                          ></app-number-input>
                        </label>
                      </div>
                    `,
                  )}
                  ${when(
                    this.hasCO2Sensor,
                    () => html`
                      <div class="form-control">
                        <label
                          for="co2-offset"
                          class="form-control__label"
                        >
                          CO<sub>2</sub> Offset
                          <app-number-input
                            class="form-control__input"
                            id="co2-offset"
                            name="co2-offset"
                            step="0.1"
                            .value=${this.sensor?.cO2LevelPpmOffset ?? 0}
                          ></app-number-input>
                        </label>
                      </div>
                    `,
                  )}
                </div>
              </app-tabs>
            </form>
          `,
        )}
        <div slot="actions">
          <button
            type="button"
            class="button button--secondary"
            @click=${() => this.modal.hide('close-button')}
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
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-sensor-settings': SensorSettings;
  }
}
