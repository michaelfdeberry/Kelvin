import { customElement } from 'lit/decorators.js';

import { signalrEvents } from '../events.js';
import { SignalRHubBase } from './signalr-hub-base.js';
import { ControlStateChange } from '../models/control-state-change.js';
import resources from '../services/api-resources.js';
import { apiGet } from '../services/api.js';
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
      const response = await apiGet<ControlStateResponse>(resources.control.getControlState);
      if (!response.lastChange) {
        console.warn('No control state change found in the response.');
        return;
      }

      // TODO this is wrong. the last change won't represent the current state of the control
      // The response will have the current state, but it won't have the full state. I'm not sure if this is what I
      // really want here. I really just need the state of the relays, anything else is just noise
      // for how this is used.
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
