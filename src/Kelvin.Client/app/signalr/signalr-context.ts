import './realtime-hub.js';

import { ContextProvider } from '@lit/context';
import { css, html, LitElement } from 'lit';
import { customElement } from 'lit/decorators.js';

import {
  callContext,
  controlContext,
  defaultCallStateChange,
  defaultControlStateChange,
  defaultLifecycleStateChange,
  defaultRelayStates,
  lifecycleContext,
  relayStatesContext,
} from '../contexts/control-context';
import { defaultEnvironmentReadings, environmentReadingsContext } from '../contexts/sensors-context';
import { signalrEvents } from '../events';
import { ControlStateChange } from '../models/control-state-change';
import { RelayStates } from '../models/gateway';
import { EnvironmentReading } from '../models/sensors';

@customElement('signalr-context')
export class SignalRContext extends LitElement {
  static override styles = css`
    :host {
      display: contents;
    }
  `;

  private callProvider = new ContextProvider(this, {
    context: callContext,
    initialValue: defaultCallStateChange,
  });

  private controlProvider = new ContextProvider(this, {
    context: controlContext,
    initialValue: defaultControlStateChange,
  });

  private lifecycleProvider = new ContextProvider(this, {
    context: lifecycleContext,
    initialValue: defaultLifecycleStateChange,
  });

  private relayStatesProvider = new ContextProvider(this, {
    context: relayStatesContext,
    initialValue: defaultRelayStates,
  });

  private sensorReadingsProvider = new ContextProvider(this, {
    context: environmentReadingsContext,
    initialValue: defaultEnvironmentReadings as EnvironmentReading,
  });

  override connectedCallback(): void {
    super.connectedCallback();

    // i'm not cleaning these up because this context will live the life of the application
    this.addEventListener(signalrEvents.readingsHub.sensorReadingsUpdated, event => {
      const customEvent = event as CustomEvent<EnvironmentReading>;
      this.sensorReadingsProvider.setValue(customEvent.detail);
    });

    this.addEventListener(signalrEvents.controlHub.controlStateChanged, event => {
      const customEvent = event as CustomEvent<ControlStateChange>;
      this.controlProvider.setValue(customEvent.detail);
    });

    // subset of control state changes that specifically pertain to call kind
    this.addEventListener(signalrEvents.controlHub.callStateChanged, event => {
      const customEvent = event as CustomEvent<ControlStateChange>;
      this.callProvider.setValue(customEvent.detail);
    });

    this.addEventListener(signalrEvents.controlHub.lifecycleStateChanged, event => {
      this.lifecycleProvider.setValue((event as CustomEvent<ControlStateChange>).detail);
    });

    this.addEventListener(signalrEvents.controlHub.relayStatesChanged, event => {
      this.relayStatesProvider.setValue((event as CustomEvent<RelayStates>).detail);
    });
  }

  override render() {
    return html`
      <slot></slot>
      <signalr-realtime-hub></signalr-realtime-hub>
    `;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions
  interface HTMLElementTagNameMap {
    'signalr-context': SignalRContext;
  }
}
