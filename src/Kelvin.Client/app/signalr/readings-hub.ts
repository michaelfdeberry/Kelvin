import { customElement } from 'lit/decorators.js';

import { signalrEvents } from '../events.js';
import { SignalRHubBase } from './signalr-hub-base.js';
import apiResources from '../services/api-resources.js';
import { apiGet } from '../services/api.js';
import { dispatchCustomEvent } from '../services/utilities.js';

import type { EnvironmentReading } from '../models/sensors.js';

const READINGS_UPDATED_HANDLER = 'ReadingsUpdated';

@customElement('signalr-readings-hub')
export class ReadingsHub extends SignalRHubBase {
  protected override readonly hubUrl = '/hubs/readings';
  protected override readonly hubName = 'readings';

  override connectedCallback(): void {
    super.connectedCallback();
    void this.loadInitialReadings();
  }

  protected override onSignalrConnected(): void {
    this.registerHubHandler<EnvironmentReading>(READINGS_UPDATED_HANDLER, signalrEvents.readingsHub.sensorReadingsUpdated);
  }

  async loadInitialReadings(): Promise<void> {
    const response = await apiGet<{ reading: EnvironmentReading }>(apiResources.sensors.getLatestReading);
    if (response) {
      dispatchCustomEvent<Partial<EnvironmentReading>>(this, signalrEvents.readingsHub.sensorReadingsUpdated, response.reading);
    }
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'signalr-readings-hub': ReadingsHub;
  }
}
