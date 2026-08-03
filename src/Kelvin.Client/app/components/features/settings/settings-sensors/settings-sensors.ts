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
import { apiFetch } from '../../../../services/api.js';
import { dispatchCustomEvent, dispatchToast } from '../../../../services/utilities.js';
import sharedStyles from '../../../../shared.styles.js';
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
        const results = await apiFetch<{ value: { readings: SensorReading[] } }>('sensors/readings/latest', { signal });
        return results?.value.readings ?? ([] as SensorReading[]);
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
      await apiFetch(`sensors/${sensorId}/enable`, { method: enabled ? 'DELETE' : 'POST' });
      dispatchToast(this, 'success', `Sensor ${enabled ? 'disabled' : 'enabled'} successfully`);
      dispatchCustomEvent(this, events.sensorsUpdated);
    } catch {
      dispatchToast(this, 'error', `Failed to ${enabled ? 'disable' : 'enable'} sensor`);
    }
  }

  private async handleSensorRemove(sensorId: string): Promise<void> {
    if (!confirm('Are you sure you want to remove this sensor?')) return;

    try {
      await apiFetch(`sensors/${sensorId}`, { method: 'DELETE' });
      dispatchToast(this, 'success', 'Sensor removed successfully');
      dispatchCustomEvent(this, events.sensorsUpdated);
    } catch {
      dispatchToast(this, 'error', 'Failed to remove sensor');
    }
  }

  private renderBattery(percentage: number): TemplateResult {
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
              <th>Last Seen</th>
              <th class="table__actions-header">Actions</th>
            </tr>
          </thead>
          <tbody>
            ${this.sensors.map(sensor => {
              const reading = readingsMap[sensor.id];
              return html`
                <tr>
                  <td>
                    <div class="sensor-info">
                      <span class="sensor-name">${sensor.name}</span>
                      ${sensor.hasBattery && reading ? this.renderBattery(reading.batteryLevelPercentage) : nothing}
                    </div>
                  </td>
                  <td>
                    <div class="features">
                      <span class="badge ${sensor.hasHumiditySensor ? 'badge--active' : ''}">💧 Humidity</span>
                      <span class="badge ${sensor.hasCO2Sensor ? 'badge--active' : ''}">☁️ CO<sub>2</sub></span>
                    </div>
                  </td>
                  <td>
                    <span class="status ${sensor.enabled ? 'status--enabled' : 'status--disabled'}"> ${sensor.enabled ? 'Active' : 'Disabled'} </span>
                  </td>
                  <td>
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
  interface HTMLElementTagNameMap {
    'app-settings-sensors': SettingsSensors;
  }
}
