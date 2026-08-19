import './components/shared/alert/alert.js';
import './components/shared/toaster/toaster.js';
import './signalr/control-hub.js';
import './signalr/readings-hub.js';
import './signalr/signalr-context.js';

import { ContextProvider } from '@lit/context';
import { html, LitElement, nothing, TemplateResult } from 'lit';
import { customElement, state } from 'lit/decorators.js';
import { when } from 'lit/directives/when.js';

import appShellStyles from './app.styles.js';
import './components/layout/app-sidebar/app-sidebar.js';
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
import { Sensors as SensorsResponse } from './models/sensors.js';
import './router.js';
import { SchedulesResponse, SetPointsResponse, Thermostat } from './models/thermostat.js';
import resources from './services/api-resources.js';
import { apiGet, apiPut } from './services/api.js';
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

  @state()
  private isThermostatDisabled: boolean | undefined = undefined;

  override connectedCallback(): void {
    super.connectedCallback();

    this.loadThermostat();
    this.loadSensors();
    this.loadPreferences();

    this.handlePreferencesSaved = this.handlePreferencesSaved.bind(this);
    this.handleSensorsUpdated = this.handleSensorsUpdated.bind(this);
    this.handleThermostatUpdated = this.handleThermostatUpdated.bind(this);

    this.addEventListener(events.preferencesSaved, this.handlePreferencesSaved);
    this.addEventListener(events.sensorsUpdated, this.handleSensorsUpdated);
    this.addEventListener(events.thermostatUpdated, this.handleThermostatUpdated);
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();

    this.removeEventListener(events.preferencesSaved, this.handlePreferencesSaved);
    this.removeEventListener(events.sensorsUpdated, this.handleSensorsUpdated);
    this.removeEventListener(events.thermostatUpdated, this.handleThermostatUpdated);
  }

  handlePreferencesSaved(event: Event): void {
    event.stopPropagation();

    const customEvent = event as CustomEvent<Preferences>;
    this.preferencesProvider.setValue(customEvent.detail);
  }

  handleSensorsUpdated(event: Event): void {
    event.stopPropagation();
    this.loadSensors();
  }

  handleThermostatUpdated(event: Event): void {
    event.stopPropagation();
    this.loadThermostat();
  }

  private async loadPreferences(): Promise<void> {
    try {
      const preferences = await apiGet<Preferences>(resources.preferences.getPreferences);
      this.preferencesProvider.setValue(preferences);
    } catch (err) {
      dispatchToast(this, 'error', 'Failed to load preferences.');
      console.error('Failed to load preferences:', err);
    }
  }

  private async loadSensors(): Promise<void> {
    try {
      const response = await apiGet<SensorsResponse>(resources.sensors.getSensors);
      this.sensorsProvider.setValue(response.sensors);
    } catch (err) {
      dispatchToast(this, 'error', 'Failed to load sensors.');
      console.error('Failed to load sensors:', err);
    }
  }

  private async loadThermostat(): Promise<void> {
    try {
      const thermostat = await apiGet<Thermostat>(resources.thermostat.getThermostat);
      this.isThermostatDisabled = thermostat.mode === 'Disabled';
      this.thermostatProvider.setValue(thermostat);

      const setPointsResponse = await apiGet<SetPointsResponse>(resources.thermostat.getSetPoints);
      this.setPointsProvider.setValue(setPointsResponse.setPoints);

      const schedulesResponse = await apiGet<SchedulesResponse>(resources.thermostat.getSchedules);
      this.schedulesProvider.setValue(schedulesResponse.schedules);
    } catch (err) {
      dispatchToast(this, 'error', 'Failed to load thermostat.');
      console.error('Failed to load thermostat:', err);
    }
  }

  private async handleTakeControlClick(): Promise<void> {
    try {
      console.log('Current thermostat mode:', this.thermostatProvider.value.mode);
      await apiPut<void>(resources.thermostat.updateThermostat, {
        body: { ...this.thermostatProvider.value, mode: 'Off' },
      });
      this.loadThermostat();
    } catch (error) {
      dispatchToast(this, 'error', 'Failed to take control of the thermostat.');
      console.error('Failed to take control of the thermostat:', error);
    }
  }

  private renderBanner(): TemplateResult | typeof nothing {
    if (this.isThermostatDisabled === undefined) return nothing;
    if (this.thermostatProvider.value.mode !== 'Disabled') return nothing;

    return html`
      <div class="app-shell__banner">
        <app-alert
          banner
          type="warning"
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
          <signalr-control-hub></signalr-control-hub>
          <signalr-readings-hub></signalr-readings-hub>
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
