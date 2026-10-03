import { ContextProvider } from '@lit/context';
import { customElement } from 'lit/decorators.js';

import { bannerContext, defaultBanner } from '../contexts/banner-context.js';
import { events, signalrEvents } from '../events.js';
import { SignalRHubBase } from './signalr-hub-base.js';
import apiResources from '../services/api-resources.js';
import { apiGet } from '../services/api.js';
import { dispatchCustomEvent } from '../services/utilities.js';

import type { ControlStateChange } from '../models/control-state-change.js';
import type { ControlStateResponse } from '../models/control-state.js';
import type { EnvironmentReading } from '../models/sensors.js';
import type { ToastDetail } from '../models/toast-detail.js';

const RECEIVE_HANDLER = 'Receive';

// Mirrors Kelvin.Server's Hubs/RealtimeHub.cs RealtimeMessageTypes - keep these literal strings in sync.
const MessageType = {
  ControlStateChanged: 'ControlStateChanged',
  ThermostatStateChanged: 'ThermostatStateChanged',
  SensorsStateChanged: 'SensorsStateChanged',
  ReadingsUpdated: 'ReadingsUpdated',
  Notify: 'Notify',
} as const;

type RealtimeMessage<T = unknown> = {
  type: string;
  payload: T;
};

type Notification = ToastDetail & { banner?: boolean };

/**
 * The single real-time pipe from the server: every push (control state, thermostat/sensors config
 * changes, environment readings, notifications) arrives as one envelope on this hub and is re-dispatched
 * here as the same DOM CustomEvents the rest of the app already listens for.
 */
@customElement('signalr-realtime-hub')
export class RealtimeHub extends SignalRHubBase {
  protected override readonly hubUrl = '/hubs/realtime';
  protected override readonly hubName = 'realtime';

  private bannerContextProvider = new ContextProvider(this, {
    context: bannerContext,
    initialValue: defaultBanner,
  });

  override connectedCallback(): void {
    super.connectedCallback();
    void this.loadInitialControlState();
    void this.loadInitialReadings();
  }

  protected override onSignalrConnected(): void {
    this.registerRawHandler<RealtimeMessage>(RECEIVE_HANDLER, message => this.handleMessage(message));
  }

  private handleMessage(message: RealtimeMessage): void {
    switch (message.type) {
      case MessageType.ControlStateChanged:
        dispatchCustomEvent<ControlStateChange>(this, signalrEvents.controlHub.controlStateChanged, message.payload as ControlStateChange);
        break;
      case MessageType.ThermostatStateChanged:
        dispatchCustomEvent(this, events.thermostatUpdated);
        break;
      case MessageType.SensorsStateChanged:
        dispatchCustomEvent(this, events.sensorsUpdated);
        break;
      case MessageType.ReadingsUpdated:
        dispatchCustomEvent<EnvironmentReading>(this, signalrEvents.readingsHub.sensorReadingsUpdated, message.payload as EnvironmentReading);
        break;
      case MessageType.Notify:
        this.handleNotify(message.payload as Notification);
        break;
      default:
        console.warn(`Unhandled realtime message type: ${message.type}`);
    }
  }

  private handleNotify(notification: Notification): void {
    if (notification.banner) {
      this.bannerContextProvider.setValue(notification);
      return;
    }

    dispatchCustomEvent<ToastDetail>(this, events.toast, notification);
  }

  private async loadInitialControlState(): Promise<void> {
    try {
      const response = await apiGet<ControlStateResponse>(apiResources.control.getControlState);
      if (!response.lastChange) {
        console.warn('No control state change found in the response.');
        return;
      }
      dispatchCustomEvent<ControlStateChange>(this, signalrEvents.controlHub.controlStateChanged, response.lastChange);
    } catch (error) {
      console.error('Failed to load control state:', error);
    }
  }

  private async loadInitialReadings(): Promise<void> {
    const response = await apiGet<{ reading: EnvironmentReading }>(apiResources.sensors.getLatestReading);
    if (response) {
      dispatchCustomEvent<Partial<EnvironmentReading>>(this, signalrEvents.readingsHub.sensorReadingsUpdated, response.reading);
    }
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'signalr-realtime-hub': RealtimeHub;
  }
}
