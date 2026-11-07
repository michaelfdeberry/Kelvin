import '../../../shared/date-time/date-time.js';
import '../../../shared/modal/modal.js';

import { consume } from '@lit/context';
import { html, LitElement, nothing, TemplateResult } from 'lit';
import { customElement, query, state } from 'lit/decorators.js';
import { unsafeSVG } from 'lit/directives/unsafe-svg.js';
import { when } from 'lit/directives/when.js';

import settingsDevicesStyles from './settings-devices.styles.js';
import editIcon from '../../../../../assets/icons/edit.svg?raw';
import powerIcon from '../../../../../assets/icons/power.svg?raw';
import trashIcon from '../../../../../assets/icons/trash.svg?raw';
import { devicesContext } from '../../../../contexts/devices-context.js';
import { events } from '../../../../events.js';
import { Device, UpdateDeviceRequest } from '../../../../models/devices.js';
import resources from '../../../../services/api-resources.js';
import { apiDelete, apiPost, apiPut } from '../../../../services/api.js';
import { dispatchCustomEvent, dispatchToast } from '../../../../services/utilities.js';
import sharedStyles from '../../../../shared.styles.js';
import { confirmModal } from '../../../shared/modal/modal-utilities.js';
import { Modal } from '../../../shared/modal/modal.js';

@customElement('app-settings-devices')
export class SettingsDevices extends LitElement {
  static override styles = [sharedStyles, settingsDevicesStyles];

  @consume({ context: devicesContext, subscribe: true })
  private devices!: Device[];

  @query('app-modal')
  private modal?: Modal;

  @state()
  private editingId?: string;

  @state()
  private isModalOpen = false;

  private get editingDevice(): Device | undefined {
    return this.devices?.find(device => device.id === this.editingId);
  }

  private handleEdit(device: Device): void {
    this.editingId = device.id;
    this.isModalOpen = true;
  }

  private async handleSave(event: Event): Promise<void> {
    event.preventDefault();

    const device = this.editingDevice;
    const form = this.renderRoot.querySelector<HTMLFormElement>('form.settings-devices__form');
    if (!device || !form) return;

    const data = new FormData(form);
    const name = String(data.get('name') ?? '').trim();

    if (!name) {
      dispatchToast(this, 'Error', 'Device name is required.');
      return;
    }

    const body: UpdateDeviceRequest = { name };

    try {
      await apiPut<void>(resources.devices.updateDevice, { body, routeParams: { id: device.id } });
      dispatchToast(this, 'Success', 'Device updated successfully.');
      dispatchCustomEvent(this, events.devicesUpdated);
      this.isModalOpen = false;
    } catch {
      // apiPut already surfaces an error toast.
    }
  }

  private async handleToggle(device: Device): Promise<void> {
    try {
      if (device.enabled) {
        await apiDelete<void>(resources.devices.disableDevice, { routeParams: { deviceId: device.id } });
      } else {
        await apiPost<void>(resources.devices.enableDevice, { body: undefined, routeParams: { deviceId: device.id } });
      }
      dispatchToast(this, 'Success', `Device ${device.enabled ? 'disabled' : 'enabled'} successfully`);
      dispatchCustomEvent(this, events.devicesUpdated);
    } catch {
      // The api helpers already surface an error toast.
    }
  }

  private async handleRestore(device: Device): Promise<void> {
    try {
      await apiPost<void>(resources.devices.restoreDevice, { body: undefined, routeParams: { id: device.id } });
      dispatchToast(this, 'Success', `Device ${device.name} restored successfully`);
      dispatchCustomEvent(this, events.devicesUpdated);
    } catch {
      // The api helpers already surface an error toast.
    }
  }

  private async handleRemove(device: Device): Promise<void> {
    if (!(await confirmModal('Are you sure you want to remove this device?'))) return;

    try {
      await apiDelete<void>(resources.devices.deleteDevice, { routeParams: { id: device.id } });
      dispatchToast(this, {
        type: 'Success',
        duration: 3000,
        message: html`
          Device ${device.name} removed successfully.
          <button
            slot="actions"
            class="button button--success button--small"
            @click=${() => this.handleRestore(device)}
          >
            Undo
          </button>
        `,
      });
      dispatchCustomEvent(this, events.devicesUpdated);
    } catch {
      // The api helpers already surface an error toast.
    }
  }

  private renderEditModal(): TemplateResult | typeof nothing {
    const device = this.editingDevice;
    if (!device) return nothing;

    return html`
      <app-modal
        ?open=${this.isModalOpen}
        heading="Edit Device"
        description="Edit the settings for ${device.name}"
        @modal-closed=${() => (this.isModalOpen = false)}
      >
        ${when(
          this.isModalOpen,
          () => html`
            <form
              class="settings-devices__form form-group"
              @submit=${this.handleSave}
            >
              <div class="form-control">
                <label class="form-control__label">
                  Device Name
                  <input
                    type="text"
                    name="name"
                    class="form-control__input input"
                    required
                    .value=${device.name}
                  />
                </label>
              </div>
            </form>
          `,
        )}
        <div slot="actions">
          <button
            type="button"
            class="button button--secondary"
            @click=${() => this.modal?.hide('close-button')}
          >
            Cancel
          </button>
          <button
            type="submit"
            class="button button--primary"
            @click=${this.handleSave}
          >
            Save
          </button>
        </div>
      </app-modal>
    `;
  }

  override render(): TemplateResult | typeof nothing {
    if (!this.devices?.length) return nothing;

    return html`
      <section
        class="settings-devices"
        aria-label="Device settings"
      >
        <article class="card">
          <h2 class="card__title">Devices</h2>
          ${this.renderEditModal()}
          <div class="table-container">
            <table class="table">
              <thead>
                <tr>
                  <th>Device</th>
                  <th>Type</th>
                  <th>Status</th>
                  <th>Address</th>
                  <th>Last Seen</th>
                  <th class="table__actions-header">Actions</th>
                </tr>
              </thead>
              <tbody>
                ${this.devices.map(
                  device => html`
                    <tr>
                      <td data-label="Device"><span class="device-name">${device.name}</span></td>
                      <td data-label="Type">${device.type}</td>
                      <td data-label="Status">
                        <span class="status ${device.enabled ? 'status--enabled' : 'status--disabled'}"
                          >${device.enabled ? 'Active' : 'Disabled'}</span
                        >
                      </td>
                      <td data-label="Address"><span class="address">${device.macAddress ?? 'N/A'}</span></td>
                      <td data-label="Last Seen">
                        <span class="last-seen">
                          ${when(
                            !!device.lastSeenAt,
                            () => html`<app-date-time .dateTime=${device.lastSeenAt!}></app-date-time>`,
                            () => html`N/A`,
                          )}
                        </span>
                      </td>
                      <td>
                        <div class="table__actions">
                          <button
                            class="button button--icon"
                            title="Edit Device"
                            @click=${() => this.handleEdit(device)}
                          >
                            ${unsafeSVG(editIcon)}
                          </button>
                          <button
                            class="button button--icon"
                            title=${device.enabled ? 'Disable Device' : 'Enable Device'}
                            @click=${() => this.handleToggle(device)}
                          >
                            ${unsafeSVG(powerIcon)}
                          </button>
                          <button
                            class="button button--icon button--danger"
                            title="Remove Device"
                            @click=${() => this.handleRemove(device)}
                          >
                            ${unsafeSVG(trashIcon)}
                          </button>
                        </div>
                      </td>
                    </tr>
                  `,
                )}
              </tbody>
            </table>
          </div>
        </article>
      </section>
    `;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-settings-devices': SettingsDevices;
  }
}
