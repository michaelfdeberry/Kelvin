import '@fontsource/jetbrains-mono';

import './components/shared/alert/alert.js';
import './components/shared/toaster/toaster.js';
import './signalr/signalr-context.js';

import { consume, ContextProvider } from '@lit/context';
import { html, LitElement, nothing, TemplateResult } from 'lit';
import { customElement, state } from 'lit/decorators.js';
import { when } from 'lit/directives/when.js';

import appShellStyles from './app.styles.js';
import { bannerContext } from './contexts/banner-context.js';
import './components/layout/app-sidebar/app-sidebar.js';
import {
  defaultDevices,
  defaultDeviceStates,
  defaultHumidifiers,
  devicesContext,
  deviceStatesContext,
  humidifiersContext,
} from './contexts/devices-context.js';
import { defaultPreferences, preferencesContext } from './contexts/preferences-context.js';
import { defaultSensors, sensorsContext } from './contexts/sensors-context.js';
import {
  thermostatContext,
  defaultThermostat,
  defaultSetPoints,
  setPointsContext,
  defaultSchedules,
  schedulesContext,
} from './contexts/thermostat-context.js';
import { events } from './events.js';
import { DevicesSettings, DeviceStates } from './models/devices.js';
import { Sensor } from './models/sensors.js';
import './router.js';
import { Thermostat, ThermostatSettings } from './models/thermostat.js';
import { ToastDetail } from './models/toast-detail.js';
import resources from './services/api-resources.js';
import { apiPut } from './services/api.js';
import { isKioskMode } from './services/kiosk.js';
import { dispatchToast } from './services/utilities.js';
import sharedStyles from './shared.styles.js';

import type { Preferences } from './models/preferences.js';

@customElement('kelvin-app')
export class KelvinApp extends LitElement {
  static override styles = [sharedStyles, appShellStyles];

  private preferencesProvider = new ContextProvider(this, {
    context: preferencesContext,
    initialValue: defaultPreferences,
  });

  private sensorsProvider = new ContextProvider(this, {
    context: sensorsContext,
    initialValue: defaultSensors,
  });

  private devicesProvider = new ContextProvider(this, {
    context: devicesContext,
    initialValue: defaultDevices,
  });

  private humidifiersProvider = new ContextProvider(this, {
    context: humidifiersContext,
    initialValue: defaultHumidifiers,
  });

  private deviceStatesProvider = new ContextProvider(this, {
    context: deviceStatesContext,
    initialValue: defaultDeviceStates,
  });

  private thermostatProvider = new ContextProvider(this, {
    context: thermostatContext,
    initialValue: defaultThermostat,
  });

  private setPointsProvider = new ContextProvider(this, {
    context: setPointsContext,
    initialValue: defaultSetPoints,
  });

  private schedulesProvider = new ContextProvider(this, {
    context: schedulesContext,
    initialValue: defaultSchedules,
  });

  @consume({ context: bannerContext, subscribe: true })
  private banner?: ToastDetail;

  @state()
  private isThermostatDisabled: boolean | undefined = undefined;

  override connectedCallback(): void {
    super.connectedCallback();

    this.handlePreferencesSaved = this.handlePreferencesSaved.bind(this);
    this.handleSensorsUpdated = this.handleSensorsUpdated.bind(this);
    this.handleDevicesUpdated = this.handleDevicesUpdated.bind(this);
    this.handleDeviceStateChanged = this.handleDeviceStateChanged.bind(this);
    this.handleThermostatUpdated = this.handleThermostatUpdated.bind(this);

    this.addEventListener(events.preferencesSaved, this.handlePreferencesSaved);
    this.addEventListener(events.sensorsUpdated, this.handleSensorsUpdated);
    this.addEventListener(events.devicesUpdated, this.handleDevicesUpdated);
    this.addEventListener(events.deviceStateChanged, this.handleDeviceStateChanged);
    this.addEventListener(events.thermostatUpdated, this.handleThermostatUpdated);
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();

    this.removeEventListener(events.preferencesSaved, this.handlePreferencesSaved);
    this.removeEventListener(events.sensorsUpdated, this.handleSensorsUpdated);
    this.removeEventListener(events.devicesUpdated, this.handleDevicesUpdated);
    this.removeEventListener(events.deviceStateChanged, this.handleDeviceStateChanged);
    this.removeEventListener(events.thermostatUpdated, this.handleThermostatUpdated);
  }

  handlePreferencesSaved(event: Event): void {
    event.stopPropagation();

    const customEvent = event as CustomEvent<Preferences>;
    this.preferencesProvider.setValue(customEvent.detail);
  }

  handleSensorsUpdated(event: Event): void {
    event.stopPropagation();
    this.sensorsProvider.setValue((event as CustomEvent<Sensor[]>).detail);
  }

  handleThermostatUpdated(event: Event): void {
    event.stopPropagation();
    this.applyThermostatSettings((event as CustomEvent<Partial<ThermostatSettings>>).detail);
  }

  handleDevicesUpdated(event: Event): void {
    event.stopPropagation();

    const { devices, humidifiers } = (event as CustomEvent<Partial<DevicesSettings>>).detail;
    if (devices) this.devicesProvider.setValue(devices);
    if (humidifiers) this.humidifiersProvider.setValue(humidifiers);
  }

  handleDeviceStateChanged(event: Event): void {
    event.stopPropagation();
    this.deviceStatesProvider.setValue((event as CustomEvent<DeviceStates>).detail);
  }

  private applyThermostatSettings({ thermostat, setPoints, schedules }: Partial<ThermostatSettings>): void {
    if (thermostat) {
      this.isThermostatDisabled = thermostat.mode === 'Disabled';
      this.thermostatProvider.setValue(thermostat);
    }

    if (setPoints) this.setPointsProvider.setValue(setPoints);
    if (schedules) this.schedulesProvider.setValue(schedules);
  }

  private async handleTakeControlClick(): Promise<void> {
    try {
      const thermostat: Thermostat = { ...this.thermostatProvider.value, mode: 'Off' };
      await apiPut<void>(resources.thermostat.updateThermostat, { body: thermostat });
      this.applyThermostatSettings({ thermostat });
    } catch (error) {
      dispatchToast(this, 'Error', 'Failed to take control of the thermostat.');
      console.error('Failed to take control of the thermostat:', error);
    }
  }

  private renderBanner(): TemplateResult | typeof nothing {
    // Banners for the context will only be shown in extreme cases, so that's going to take precedence over the thermostat disabled banner.
    // Currently, the only use case is when all the sensors are offline.
    if (this.banner) {
      return html`
        <div class="app-shell__banner">
          <app-alert
            banner
            type=${this.banner.type}
            heading=${this.banner.heading ?? ''}
          >
            <p>${this.banner.message}</p>
          </app-alert>
        </div>
      `;
    }

    if (this.isThermostatDisabled) {
      return html`
        <div class="app-shell__banner">
          <app-alert
            banner
            type="Warning"
            heading="Kelvin is Disabled"
          >
            <p>The HVAC system is being controlled by the failsafe thermostat.</p>
            <button
              slot="actions"
              class="button button--warning button--small"
              @click=${this.handleTakeControlClick}
            >
              Take Control
            </button>
          </app-alert>
        </div>
      `;
    }

    return nothing;
  }

  override render() {
    return html`
      <signalr-context>
        ${this.renderBanner()}
        <div class="app-shell__shell">
          ${when(!isKioskMode(), () => html`<app-sidebar></app-sidebar>`)}
          <main class="app-shell__main">
            <app-router></app-router>
          </main>
          <app-toaster></app-toaster>
        </div>
      </signalr-context>
    `;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'kelvin-app': KelvinApp;
  }
}
