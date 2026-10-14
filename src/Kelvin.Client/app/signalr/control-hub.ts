import { customElement } from 'lit/decorators.js';

import { events, signalrEvents } from '../events.js';
import { SignalRHubBase } from './signalr-hub-base.js';
import { ControlStateChange } from '../models/control-state-change.js';
import resources from '../services/api-resources.js';
import { apiGet } from '../services/api.js';
import { dispatchCustomEvent } from '../services/utilities.js';

import type { ControlStateResponse } from '../models/control-state.js';

const CONTROL_STATE_CHANGED_HANDLER = 'ControlStateChanged';
const THERMOSTAT_STATE_CHANGED_HANDLER = 'ThermostatStateChanged';
const SENSORS_STATE_CHANGED_HANDLER = 'SensorsStateChanged';

@customElement('signalr-control-hub')
export class ControlHub extends SignalRHubBase {
  protected override readonly hubUrl = '/hubs/control';
  protected override readonly hubName = 'control';

  override connectedCallback(): void {
    super.connectedCallback();
    void this.loadInitialState();
  }

  protected override onSignalrConnected(): void {
    this.registerHubHandler<ControlStateChange>(CONTROL_STATE_CHANGED_HANDLER, signalrEvents.controlHub.controlStateChanged);
    this.registerHubHandler<ControlStateChange>(CONTROL_STATE_CHANGED_HANDLER, signalrEvents.controlHub.callStateChanged, data => {
      if (data.kind === 'Call') {
        return data;
      }
    });
    this.registerHubHandler<void>(THERMOSTAT_STATE_CHANGED_HANDLER, events.thermostatUpdated);
    this.registerHubHandler<void>(SENSORS_STATE_CHANGED_HANDLER, events.sensorsUpdated);
  }

  private async loadInitialState(): Promise<void> {
    try {
      // last change will now always contain the last call control change
      const response = await apiGet<ControlStateResponse>(resources.control.getControlState);
      if (!response.lastChange) {
        console.warn('No control state change found in the response.');
        return;
      }
      dispatchCustomEvent<ControlStateChange>(this, signalrEvents.controlHub.controlStateChanged, response.lastChange);
    } catch (error) {
      console.error('Failed to load control state:', error);
    }
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'signalr-control-hub': ControlHub;
  }
}
