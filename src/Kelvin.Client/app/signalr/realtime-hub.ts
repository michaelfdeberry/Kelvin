import { ContextProvider } from '@lit/context';
import { customElement } from 'lit/decorators.js';

import { bannerContext, defaultBanner } from '../contexts/banner-context.js';
import { events, signalrEvents } from '../events.js';
import { SignalRHubBase } from './signalr-hub-base.js';
import { Preferences } from '../models/preferences.js';
import { dispatchCustomEvent, dispatchToast } from '../services/utilities.js';

import type { ControlStateChange } from '../models/control-state-change.js';
import type { DevicesSettings, DeviceStates } from '../models/devices.js';
import type { EnvironmentReading, Sensor } from '../models/sensors.js';
import type { ThermostatSettings } from '../models/thermostat.js';
import type { ToastDetail } from '../models/toast-detail.js';

const RECEIVE_HANDLER = 'Receive';

// Mirrors Kelvin.Server's Hubs/RealtimeHub.cs RealtimeMessageTypes - keep these literal strings in sync.
const MessageType = {
  ControlStateChanged: 'ControlStateChanged',
  ThermostatSettingsChanged: 'ThermostatSettingsChanged',
  SensorsSettingsChanged: 'SensorsSettingsChanged',
  DevicesSettingsChanged: 'DevicesSettingsChanged',
  DeviceStateChanged: 'DeviceStateChanged',
  EnvironmentReadingChanged: 'EnvironmentReadingChanged',
  NotificationSent: 'NotificationSent',
  NotificationDismissed: 'NotificationDismissed',
  PreferencesChanged: 'PreferencesChanged',
} as const;

type RealtimeMessage<T = unknown> = {
  type: string;
  payload: T;
};

type Notification = ToastDetail & { id?: string; banner?: boolean };

/**
 * The single real-time pipe from the server: everything is pushed on connect (and again on reconnect), then every change after that
 * (control state, thermostat/sensors/devices data, environment readings, notifications) arrives as one envelope on this hub and is
 * re-dispatched here as the same DOM CustomEvents the rest of the app already listens for.
 */
@customElement('signalr-realtime-hub')
export class RealtimeHub extends SignalRHubBase {
  protected override readonly hubUrl = '/hubs/realtime';
  protected override readonly hubName = 'realtime';

  private bannerContextProvider = new ContextProvider(this, {
    context: bannerContext,
    initialValue: defaultBanner,
  });

  protected override onSignalrConnected(): void {
    this.registerRawHandler<RealtimeMessage>(RECEIVE_HANDLER, message => this.handleMessage(message));
  }

  private handleMessage(message: RealtimeMessage): void {
    switch (message.type) {
      case MessageType.ControlStateChanged: {
        const controlStateChange = message.payload as ControlStateChange;
        dispatchCustomEvent<ControlStateChange>(this, signalrEvents.controlHub.controlStateChanged, controlStateChange);

        if (controlStateChange.kind === 'Call') {
          dispatchCustomEvent<ControlStateChange>(this, signalrEvents.controlHub.callStateChanged, controlStateChange);
        }

        break;
      }
      case MessageType.ThermostatSettingsChanged:
        dispatchCustomEvent<ThermostatSettings>(this, events.thermostatUpdated, message.payload as ThermostatSettings);
        break;
      case MessageType.SensorsSettingsChanged:
        dispatchCustomEvent<Sensor[]>(this, events.sensorsUpdated, message.payload as Sensor[]);
        break;
      case MessageType.DevicesSettingsChanged:
        dispatchCustomEvent<DevicesSettings>(this, events.devicesUpdated, message.payload as DevicesSettings);
        break;
      case MessageType.DeviceStateChanged:
        dispatchCustomEvent<DeviceStates>(this, events.deviceStateChanged, message.payload as DeviceStates);
        break;
      case MessageType.PreferencesChanged:
        dispatchCustomEvent(this, events.preferencesSaved, message.payload as Preferences);
        break;
      case MessageType.EnvironmentReadingChanged:
        dispatchCustomEvent<EnvironmentReading>(this, signalrEvents.readingsHub.sensorReadingsUpdated, message.payload as EnvironmentReading);
        break;
      case MessageType.NotificationSent:
        this.handleNotify(message.payload as Notification);
        break;
      case MessageType.NotificationDismissed:
        this.handleDismissNotification(message.payload as string);
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

    dispatchToast(this, notification);
  }

  private handleDismissNotification(notificationId: string): void {
    const banner = this.bannerContextProvider.value as Notification;
    if (banner?.id === notificationId) {
      this.bannerContextProvider.setValue(undefined);
      return;
    }

    dispatchCustomEvent<string>(this, events.clearToast, notificationId);
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'signalr-realtime-hub': RealtimeHub;
  }
}
