import { customElement } from 'lit/decorators.js';

import { signalrEvents } from '../events.js';
import { SignalRHubBase } from './signalr-hub-base.js';

import type { EnvironmentReading } from '../models/sensors.js';

const READINGS_UPDATED_HANDLER = 'ReadingsUpdated';

@customElement('signalr-readings-hub')
export class ReadingsHub extends SignalRHubBase {
  protected override readonly hubUrl = '/hubs/readings';
  protected override readonly hubName = 'readings';

  protected override onSignalrConnected(): void {
    this.registerHubHandler<EnvironmentReading>(READINGS_UPDATED_HANDLER, signalrEvents.readingsHub.sensorReadingsUpdated);
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'signalr-readings-hub': ReadingsHub;
  }
}
