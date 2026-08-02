import { customElement } from 'lit/decorators.js';

import { signalrEvents } from '../events.js';
import { SignalRHubBase } from './signalr-hub-base.js';
import { ControlStateChange } from '../models/control-state-change.js';
import { apiFetch } from '../services/api.js';
import { dispatchCustomEvent } from '../services/utilities.js';

import type { ControlStateResponse } from '../models/control-state.js';

const CONTROL_STATE_CHANGED_HANDLER = 'ControlStateChanged';

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
  }

  private async loadInitialState(): Promise<void> {
    try {
      const response = await apiFetch<ControlStateResponse>('control/state');
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
  interface HTMLElementTagNameMap {
    'signalr-control-hub': ControlHub;
  }
}
