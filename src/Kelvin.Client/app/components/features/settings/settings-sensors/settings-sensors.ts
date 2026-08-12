import '../../../shared/date-time/date-time.js';
import '../../sensors/sensor-settings/sensor-settings.js';

import { consume } from '@lit/context';
import { Task } from '@lit/task';
import { html, LitElement, nothing, TemplateResult } from 'lit';
import { customElement, query } from 'lit/decorators.js';
import { unsafeSVG } from 'lit/directives/unsafe-svg.js';
import { when } from 'lit/directives/when.js';

import settingsSensorsStyles from './settings-sensors.styles';
import editIcon from '../../../../../assets/icons/edit.svg?raw';
import powerIcon from '../../../../../assets/icons/power.svg?raw';
import trashIcon from '../../../../../assets/icons/trash.svg?raw';
import { sensorsContext } from '../../../../contexts/sensors-context.js';
import { events } from '../../../../events.js';
import { Sensor, SensorReading } from '../../../../models/sensors.js';
import resources from '../../../../services/api-resources.js';
import { apiDelete, apiGet, apiPost } from '../../../../services/api.js';
import { dispatchCustomEvent, dispatchToast } from '../../../../services/utilities.js';
import sharedStyles from '../../../../shared.styles.js';
import { confirmModal } from '../../../shared/modal/modal-utilities.js';
import { SensorSettings } from '../../sensors/sensor-settings/sensor-settings.js';

@customElement('app-settings-sensors')
export class SettingsSensors extends LitElement {
  static override styles = [sharedStyles, settingsSensorsStyles];

  @consume({ context: sensorsContext, subscribe: true })
  sensors!: Sensor[];

  @query('app-sensor-settings')
  sensorSettings?: SensorSettings;

  private readingsTask = new Task(this, {
    task: async (_, { signal }) => {
      try {
        const results = await apiGet<{ readings: SensorReading[] }>(resources.sensors.getLatestReadings, { signal });
        return results?.readings ?? ([] as SensorReading[]);
      } catch {
        return [] as SensorReading[];
      }
    },
    args: () => [],
  });

  private handleSensorSelected(sensorId: string) {
    if (!this.sensorSettings) return;
    this.sensorSettings.sensorId = sensorId;
    this.sensorSettings.open();
  }

  private async handleSensorToggle(sensorId: string, enabled: boolean): Promise<void> {
    try {
      if (enabled) {
        await apiDelete<void>(resources.sensors.disableSensor, { routeParams: { sensorId } });
      } else {
        await apiPost<void>(resources.sensors.enableSensor, { body: undefined, routeParams: { sensorId } });
      }
      dispatchToast(this, 'success', `Sensor ${enabled ? 'disabled' : 'enabled'} successfully`);
      dispatchCustomEvent(this, events.sensorsUpdated);
    } catch {
      dispatchToast(this, 'error', `Failed to ${enabled ? 'disable' : 'enable'} sensor`);
    }
  }

  private async handleSensorRestore(sensor: Sensor): Promise<void> {
    try {
      await apiPost<void>(resources.sensors.restoreSensor, { body: undefined, routeParams: { id: sensor.id } });
      dispatchToast(this, 'success', `Sensor ${sensor.name} restored successfully`);
      dispatchCustomEvent(this, events.sensorsUpdated);
    } catch {
      dispatchToast(this, 'error', 'Failed to restore sensor');
    }
  }

  private async handleSensorRemove(sensorId: string): Promise<void> {
    if (!(await confirmModal('Are you sure you want to remove this sensor?'))) return;

    try {
      const sensor = this.sensors.find(s => s.id === sensorId);
      if (!sensor) return;

      await apiDelete<void>(resources.sensors.deleteSensor, { routeParams: { id: sensor.id } });
      dispatchToast(this, {
        type: 'success',
        message: html`
          Sensor ${sensor.name} removed successfully.
          <button
            class="button button--success button--small"
            @click=${() => this.handleSensorRestore(sensor!)}
          >
            Undo
          </button>
        `,
      });
      dispatchCustomEvent(this, events.sensorsUpdated);
    } catch (error) {
      console.error('Failed to remove sensor:', error);
      dispatchToast(this, 'error', 'Failed to remove sensor');
    }
  }

  private renderBattery(percentage?: number): TemplateResult | typeof nothing {
    if (percentage === undefined || percentage === null) {
      return nothing;
    }

    let levelClass = 'battery-pill--high';
    let icon = '🔋';
    if (percentage <= 20) {
      levelClass = 'battery-pill--low';
      icon = '🪫';
    } else if (percentage <= 50) {
      levelClass = 'battery-pill--med';
      icon = '🔋';
    }

    return html` <span class="battery-pill ${levelClass}"> ${icon} ${percentage.toFixed(1)}% </span> `;
  }

  private renderSensors(readings: SensorReading[]): TemplateResult {
    const readingsMap = readings.reduce(
      (acc, reading) => {
        acc[reading.sensorId] = reading;
        return acc;
      },
      {} as Record<string, SensorReading>,
    );

    return html`
      <app-sensor-settings></app-sensor-settings>
      <div class="table-container">
        <table class="table">
          <thead>
            <tr>
              <th>Sensor</th>
              <th>Capabilities</th>
              <th>Status</th>
              <th>Address</th>
              <th>Last Seen</th>
              <th class="table__actions-header">Actions</th>
            </tr>
          </thead>
          <tbody>
            ${this.sensors.map(sensor => {
              const reading = readingsMap[sensor.id];
              const batteryLevel = reading?.batteryLevelPercentage ?? null;
              return html`
                <tr>
                  <td data-label="Sensor">
                    <div class="sensor-info">
                      <span class="sensor-name">${sensor.name}</span>
                      ${sensor.hasBattery && batteryLevel !== null ? this.renderBattery(batteryLevel) : nothing}
                    </div>
                  </td>
                  <td data-label="Capabilities">
                    <div class="features">
                      <span class="badge ${sensor.hasHumiditySensor ? 'badge--active' : ''}">💧 Humidity</span>
                      <span class="badge ${sensor.hasCO2Sensor ? 'badge--active' : ''}">☁️ CO<sub>2</sub></span>
                    </div>
                  </td>
                  <td data-label="Status">
                    <span class="status ${sensor.enabled ? 'status--enabled' : 'status--disabled'}"> ${sensor.enabled ? 'Active' : 'Disabled'} </span>
                  </td>
                  <td data-label="Address">
                    <span class="address">${sensor.macAddress}</span>
                  </td>
                  <td data-label="Last Seen">
                    <span class="last-seen">
                      ${when(
                        !!reading,
                        () => html`<app-date-time .dateTime=${reading!.createdAt}></app-date-time>`,
                        () => html`N/A`,
                      )}
                    </span>
                  </td>
                  <td>
                    <div class="table__actions">
                      <button
                        class="button button--icon"
                        title="Edit Sensor"
                        @click=${() => this.handleSensorSelected(sensor.id)}
                      >
                        ${unsafeSVG(editIcon)}
                      </button>
                      <button
                        class="button button--icon"
                        title=${sensor.enabled ? 'Disable Sensor' : 'Enable Sensor'}
                        @click=${() => this.handleSensorToggle(sensor.id, sensor.enabled)}
                      >
                        ${unsafeSVG(powerIcon)}
                      </button>
                      <button
                        class="button button--icon button--danger"
                        title="Remove Sensor"
                        @click=${() => this.handleSensorRemove(sensor.id)}
                      >
                        ${unsafeSVG(trashIcon)}
                      </button>
                    </div>
                  </td>
                </tr>
              `;
            })}
          </tbody>
        </table>
      </div>
    `;
  }

  override render(): TemplateResult {
    return html`
      <section
        class="settings-sensors"
        role="tabpanel"
        aria-label="Sensors settings"
      >
        <article class="card">
          <h2 class="card__title">Sensors</h2>
          ${this.readingsTask.render({
            pending: () => html`<p>Loading sensors...</p>`,
            complete: readings => this.renderSensors(readings),
          })}
        </article>
      </section>
    `;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-settings-sensors': SettingsSensors;
  }
}
